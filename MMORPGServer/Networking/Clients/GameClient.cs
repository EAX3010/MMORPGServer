using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Entities;
using MMORPGServer.Networking.Packets.Core;
using MMORPGServer.Networking.Security;
using MMORPGServer.Services; // Add this using directive to access GameRuntime
using Serilog;
using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices; // Added for MethodImplOptions
using System.Text;
using System.Threading.Channels;

namespace MMORPGServer.Networking.Clients
{
    public sealed class GameClient : IDisposable
    {
        #region Constants
        // Increased buffer size for potentially fewer read/write calls, balancing memory and I/O
        private const int MAX_PACKET_SIZE = 1024;
        private const int RECEIVE_BUFFER_SIZE = 16384; // Doubled from 8192 for fewer socket reads
        private const int SEND_BUFFER_SIZE = 16384;   // Doubled from 8192 for fewer socket writes
        private const int PACKET_LENGTH_SIZE = 2;
        private const int PACKET_SIGNATURE_SIZE = 8;
        private const int MIN_PACKET_SIZE = PACKET_LENGTH_SIZE + PACKET_SIGNATURE_SIZE;
        private const int MAX_CONSECUTIVE_ERRORS = 5;

        // Timeouts
        private static readonly TimeSpan HANDSHAKE_TIMEOUT = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan IDLE_TIMEOUT = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan WOULD_BLOCK_RETRY_DELAY = TimeSpan.FromMilliseconds(10);
        #endregion

        #region Properties
        public int ClientId { get; }
        public int PlayerId { get; set; }
        public bool IsConnected => State != ClientState.Disconnected && _tcpClient?.Connected == true;
        public string IPAddress { get; }
        public DateTime ConnectedAt { get; }
        public ClientState State { get; private set; }
        public DateTime LastActivityTime { get; private set; }
        public IPlayerNetworkContext? PlayerContext { get; set; }
        public Player? Player => PlayerContext is Player ? PlayerContext as Player : null;

        #endregion

        #region Private Fields
        private readonly TcpClient _tcpClient;
        private readonly Socket _socket;
        private readonly DiffieHellmanKeyExchange _dhKeyExchange;
        private readonly TQCast5Cryptographer _cryptographer;
        private readonly ChannelWriter<ClientMessage> _messageWriter;

        private readonly Channel<ReadOnlyMemory<byte>> _sendChannel;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        // Use ArrayPool for large, temporary buffers to reduce GC pressure
        private readonly byte[] _receiveBufferBytes;
        private readonly Memory<byte> _receiveBuffer;
        private int _receiveBufferOffset = 0;

        // For encrypted packet processing - tracks decrypted data
        private readonly byte[] _decryptedBufferBytes;
        private readonly Memory<byte> _decryptedBuffer;
        private int _decryptedBufferOffset = 0;

        // Performance tracking
        private long _packetsReceived;
        private long _packetsSent;
        private long _bytesReceived;
        private long _bytesSent;
        private readonly Stopwatch _connectionTimer = new();

        // State management
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private bool _disposed;

        // Security
        private int _consecutiveErrors;
        private DateTime _handshakeStartTime;
        #endregion

        #region Constructor
        public GameClient(
            int clientId,
            TcpClient tcpClient,
            DiffieHellmanKeyExchange dhKeyExchange,
            TQCast5Cryptographer cryptographer,
            ChannelWriter<ClientMessage> messageWriter)
        {
            ClientId = clientId;
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _socket = tcpClient.Client;
            _dhKeyExchange = dhKeyExchange ?? throw new ArgumentNullException(nameof(dhKeyExchange));
            _cryptographer = cryptographer ?? throw new ArgumentNullException(nameof(cryptographer));
            _messageWriter = messageWriter ?? throw new ArgumentNullException(nameof(messageWriter));

            IPAddress = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown"; // Handle null IP
            ConnectedAt = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            State = ClientState.Connecting;

            // Rent buffers from ArrayPool to reduce GC allocations
            _receiveBufferBytes = ArrayPool<byte>.Shared.Rent(RECEIVE_BUFFER_SIZE);
            _receiveBuffer = _receiveBufferBytes;
            _decryptedBufferBytes = ArrayPool<byte>.Shared.Rent(MAX_PACKET_SIZE);
            _decryptedBuffer = _decryptedBufferBytes;

            // Configure send channel with bounded capacity to prevent memory issues
            _sendChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait // Wait if channel is full
            });

            ConfigureSocket();
            _connectionTimer.Start();
        }

        /// <summary>
        /// Configures the underlying socket for optimal performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConfigureSocket()
        {
            try
            {
                _socket.NoDelay = true; // Disable Nagle's algorithm for lower latency
                _socket.SendBufferSize = SEND_BUFFER_SIZE;
                _socket.ReceiveBufferSize = RECEIVE_BUFFER_SIZE;
                _socket.LingerState = new LingerOption(false, 0); // Don't linger on close
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _socket.Blocking = false; // Non-blocking socket for async operations
                _socket.SendTimeout = 5000; // Timeout for synchronous send operations (though async is preferred)
                _socket.ReceiveTimeout = 5000; // Timeout for synchronous receive operations
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to configure socket options for client {ClientId}", ClientId);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts the client's asynchronous processing loops for incoming/outgoing data and health monitoring.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Create a linked CancellationTokenSource to allow internal cancellation
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token);

            // Start all processing tasks concurrently
            Task[] processTasks = new[]
            {
                ProcessIncomingDataAsync(linkedCts.Token),
                ProcessOutgoingPacketsAsync(linkedCts.Token),
                MonitorConnectionHealthAsync(linkedCts.Token)
            };

            try
            {
                _handshakeStartTime = DateTime.UtcNow;
                await InitializeConnectionAsync(); // Perform initial handshake
                await Task.WhenAny(processTasks); // Wait for any task to complete (or fail)
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Client {ClientId} operation cancelled", ClientId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error in client {ClientId}", ClientId);
            }
            finally
            {
                // Ensure disconnect is called regardless of how the tasks complete
                await DisconnectAsync("Connection terminated");
            }
        }

        /// <summary>
        /// Queues a packet to be sent to the client.
        /// </summary>
        /// <param name="packetData">The raw packet data to send.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData)
        {
            // Do not send if client is disposed or disconnected
            if (_disposed || State == ClientState.Disconnected)
                return;

            // Log if packet exceeds maximum allowed size
            if (packetData.Length > MAX_PACKET_SIZE)
            {
                Log.Error("Attempted to send packet larger than MAX_PACKET_SIZE ({PacketLength} > {MaxPacketSize})",
                    packetData.Length, MAX_PACKET_SIZE);
                return;
            }

            try
            {
                // Write the packet data to the send channel. This is non-blocking.
                await _sendChannel.Writer.WriteAsync(packetData, _cancellationTokenSource.Token);
                Interlocked.Increment(ref _packetsSent); // Increment sent packet count atomically
            }
            catch (ChannelClosedException)
            {
                // This exception is expected if the channel is closed during client disconnection
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to queue packet for client {ClientId}", ClientId);
            }
        }

        /// <summary>
        /// Disconnects the client, performs cleanup, and optionally saves player data.
        /// </summary>
        /// <param name="reason">The reason for disconnection.</param>
        /// <param name="immediate">If true, forces an immediate socket shutdown.</param>
        public async ValueTask DisconnectAsync(string reason = "", bool immediate = false)
        {
            // Use a semaphore to ensure only one disconnect operation runs at a time
            await _stateLock.WaitAsync();
            try
            {
                // If already disconnected, do nothing
                if (State == ClientState.Disconnected)
                    return;

                var previousState = State;
                State = ClientState.Disconnected; // Set state to disconnected
                _connectionTimer.Stop(); // Stop timing the connection duration
                                         // Use PlayerContext for logging
                Log.Information("Disconnecting client {ClientId} (Player: {PlayerName}): {Reason} (Duration: {Duration}, Packets R/S: {Received}/{Sent}, Bytes R/S: {BytesReceived}/{BytesSent})",
                    ClientId, Player?.Name ?? "N/A", reason, _connectionTimer.Elapsed, _packetsReceived, _packetsSent, _bytesReceived, _bytesSent);


                if (Player is not null)
                {
                    try
                    {
                        Log.Debug("Saving player {PlayerName} (ID: {PlayerId}) on disconnect...", Player.Name, Player.Id);
                        if (GameRuntime.IsInitialized)
                        {
                            await GameRuntime.GameWorld.PlayerManager.UpdatePlayerAsync(Player);
                            Log.Information("Player {PlayerName} (ID: {PlayerId}) successfully saved on disconnect.", Player.Name, Player.Id);
                        }
                        else
                        {
                            Log.Warning("GameRuntime not initialized, skipping player save for {PlayerName} on disconnect.", Player.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to save player {PlayerName} (ID: {PlayerId}) on disconnect.", Player.Name, Player.Id);
                    }
                }
                // Step 1: Cancel all ongoing operations for this client
                _cancellationTokenSource.Cancel();

                // Step 2: Complete the send channel to prevent new packets from being queued
                _sendChannel.Writer.TryComplete();

                // Step 3: If not immediate, allow a brief moment for any pending sends to complete
                if (!immediate && previousState == ClientState.Connected)
                {
                    try
                    {
                        await Task.Delay(100, CancellationToken.None);
                    }
                    catch { /* Ignore cancellation during this brief delay */ }
                }

                // Step 4: Close the TCP connection
                try
                {
                    if (immediate)
                    {
                        _socket?.Shutdown(SocketShutdown.Both); // Force close both send and receive
                    }
                    else
                    {
                        _socket?.Shutdown(SocketShutdown.Send); // Gracefully close send, allow pending receives
                        await Task.Delay(50, CancellationToken.None); // Brief wait for graceful shutdown
                    }
                    _tcpClient?.Close(); // Close the underlying TcpClient
                }
                catch (Exception ex)
                {
                    Log.Debug("Error closing socket for client {ClientId}: {Error}", ClientId, ex.Message);
                }

                // Step 5: Clear sensitive cryptographic data
                try
                {
                    _cryptographer?.Reset();
                }
                catch { /* Ignore errors during cryptographer reset */ }
            }
            finally
            {
                _stateLock.Release(); // Release the semaphore
            }
        }

        /// <summary>
        /// Initiates a graceful client disconnection.
        /// </summary>
        /// <param name="reason">The reason for disconnection.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask DisconnectGracefullyAsync(string reason = "")
            => DisconnectAsync(reason, immediate: false);

        /// <summary>
        /// Initiates an immediate client disconnection.
        /// </summary>
        /// <param name="reason">The reason for disconnection.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask DisconnectImmediatelyAsync(string reason = "")
            => DisconnectAsync(reason, immediate: true);

        /// <summary>
        /// Disconnects the client due to a security violation.
        /// </summary>
        /// <param name="violation">Details of the security violation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask DisconnectSecurityViolationAsync(string violation)
        {
            Log.Warning("Security violation from client {ClientId}: {Violation}", ClientId, violation);
            return DisconnectAsync($"Security violation: {violation}", immediate: true);
        }

        /// <summary>
        /// Disposes of managed and unmanaged resources held by the GameClient.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Ensure the client is disconnected before disposing resources.
            // Using GetAwaiter().GetResult() here to make Dispose synchronous,
            // as per common IDisposable patterns, but be aware of potential deadlocks
            // if this is called from a synchronous context that is blocked on an async operation.
            if (State != ClientState.Disconnected)
            {
                DisconnectAsync("Disposed").GetAwaiter().GetResult();
            }

            // Dispose of CancellationTokenSource, SemaphoreSlim, and TcpClient
            _cancellationTokenSource?.Dispose();
            _stateLock?.Dispose();
            _tcpClient?.Dispose();

            // Return rented buffers to the ArrayPool
            if (_receiveBufferBytes != null)
            {
                ArrayPool<byte>.Shared.Return(_receiveBufferBytes);
            }
            if (_decryptedBufferBytes != null)
            {
                ArrayPool<byte>.Shared.Return(_decryptedBufferBytes);
            }

            Log.Debug("Client {ClientId} resources disposed.", ClientId);
        }
        #endregion

        #region Connection Initialization
        /// <summary>
        /// Initializes the client connection by sending the Diffie-Hellman key exchange request.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task InitializeConnectionAsync()
        {
            await SendDHKeyExchangeAsync();
            await UpdateStateAsync(ClientState.WaitingForDummyPacket);
        }

        /// <summary>
        /// Generates and sends the Diffie-Hellman key exchange packet to the client.
        /// </summary>
        private async Task SendDHKeyExchangeAsync()
        {
            try
            {
                // Generate a default key for the cryptographer
                byte[] defaultKey = Encoding.ASCII.GetBytes("R3Xx97ra5j8D6uZz");
                _cryptographer.GenerateKey(defaultKey);

                // Create the DH key exchange packet
                ReadOnlyMemory<byte> memory = _dhKeyExchange.CreateKeyExchangePacket();

                // Rent a temporary buffer for encryption
                byte[] encryptedPacketBuffer = ArrayPool<byte>.Shared.Rent(memory.Length);

                try
                {
                    // Encrypt the DH packet and send it
                    _cryptographer.Encrypt(memory.Span, encryptedPacketBuffer.AsSpan(0, memory.Length));
                    await SendDataWithRetryAsync(new ReadOnlyMemory<byte>(encryptedPacketBuffer, 0, memory.Length));
                }
                finally
                {
                    // Return the rented buffer
                    ArrayPool<byte>.Shared.Return(encryptedPacketBuffer);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send DH key exchange for client {ClientId}", ClientId);
                throw; // Re-throw to propagate the error and trigger disconnection
            }
        }
        #endregion

        #region Outgoing Data Processing
        /// <summary>
        /// Continuously processes outgoing packets from the send channel.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        private async Task ProcessOutgoingPacketsAsync(CancellationToken cancellationToken)
        {
            // Read all packets from the channel as they become available
            await foreach (ReadOnlyMemory<byte> packetMemory in _sendChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    // Prepare the data (encrypt if necessary)
                    ReadOnlyMemory<byte> dataToSend = PrepareOutgoingData(packetMemory);

                    // Send the data over the socket with retry logic
                    await SendDataWithRetryAsync(dataToSend, cancellationToken);

                    Interlocked.Add(ref _bytesSent, dataToSend.Length); // Atomically update bytes sent
                    UpdateActivity(); // Update last activity time
                }
                catch (Exception ex) when (!ShouldDisconnectOnError(ex))
                {
                    // Log non-fatal errors and continue processing
                    Log.Debug("Non-fatal send error for client {ClientId}: {Error}", ClientId, ex.Message);
                    continue;
                }
                catch (Exception ex)
                {
                    // Log fatal errors and trigger client disconnection
                    Log.Error(ex, "Fatal send error for client {ClientId}", ClientId);
                    await DisconnectAsync($"Send error: {ex.GetType().Name}");
                    break; // Exit the loop on fatal error
                }
            }
        }

        /// <summary>
        /// Sends data over the socket with retry logic for "WouldBlock" errors.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        private async Task SendDataWithRetryAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            int retryCount = 0;
            const int maxRetries = 3; // Maximum number of retries for WouldBlock errors

            while (retryCount < maxRetries)
            {
                try
                {
                    await _socket.SendAsync(data, SocketFlags.None, cancellationToken);
                    return; // Data sent successfully
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw; // Re-throw if max retries reached

                    Log.Debug("Socket would block on send for client {ClientId}, retry {Retry}/{MaxRetries}",
                        ClientId, retryCount, maxRetries);

                    // Exponential backoff for retries
                    await Task.Delay(WOULD_BLOCK_RETRY_DELAY * retryCount, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Prepares outgoing packet data, encrypting it if the cryptographer is initialized.
        /// </summary>
        /// <param name="packetData">The raw packet data.</param>
        /// <returns>The prepared (potentially encrypted) data.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlyMemory<byte> PrepareOutgoingData(ReadOnlyMemory<byte> packetData)
        {
            // If client is connected and encryption is enabled, encrypt the packet
            if (State == ClientState.Connected && _cryptographer.IsInitialized)
            {
                // Rent a temporary buffer for encryption
                byte[] encryptedMemory = ArrayPool<byte>.Shared.Rent(packetData.Length);
                try
                {
                    _cryptographer.Encrypt(packetData.Span, encryptedMemory.AsSpan(0, packetData.Length));
                    // Return a copy of the encrypted data, as the rented buffer will be returned later
                    return new ReadOnlyMemory<byte>(encryptedMemory.AsSpan(0, packetData.Length).ToArray());
                }
                finally
                {
                    // Return the rented buffer to the pool
                    ArrayPool<byte>.Shared.Return(encryptedMemory);
                }
            }

            return packetData; // Return original data if no encryption is needed
        }
        #endregion

        #region Incoming Data Processing
        /// <summary>
        /// Continuously processes incoming data from the client's socket.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        private async Task ProcessIncomingDataAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Attempt to receive data from the socket
                    int bytesRead = await ReceiveDataWithRetryAsync(cancellationToken);
                    if (bytesRead == 0) // Connection closed gracefully by remote end
                    {
                        Log.Debug("Client {ClientId} closed connection gracefully", ClientId);
                        break;
                    }

                    _receiveBufferOffset += bytesRead; // Update buffer offset
                    Interlocked.Add(ref _bytesReceived, bytesRead); // Atomically update bytes received
                    UpdateActivity(); // Update last activity time

                    ProcessReceivedData(); // Process any complete packets in the buffer
                    ResetErrorCount(); // Reset consecutive error count on successful read
                }
                catch (Exception ex) when (!ShouldDisconnectOnError(ex))
                {
                    // Handle non-fatal errors (e.g., WouldBlock)
                    IncrementErrorCount();
                    Log.Debug("Non-fatal receive error for client {ClientId}: {Error}", ClientId, ex.Message);

                    if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                    {
                        // Disconnect if too many consecutive non-fatal errors occur
                        await DisconnectAsync("Too many consecutive errors");
                        break;
                    }

                    await Task.Delay(WOULD_BLOCK_RETRY_DELAY, cancellationToken); // Brief delay before retrying
                    continue;
                }
                catch (Exception ex)
                {
                    // Handle fatal errors (e.g., socket closed, unexpected exceptions)
                    Log.Error(ex, "Fatal receive error for client {ClientId}", ClientId);
                    await DisconnectAsync($"Receive error: {ex.GetType().Name}");
                    break; // Exit the loop on fatal error
                }
            }
        }

        /// <summary>
        /// Receives data from the socket with retry logic for "WouldBlock" errors.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>The number of bytes read, or 0 if no data was available (WouldBlock).</returns>
        private async Task<int> ReceiveDataWithRetryAsync(CancellationToken cancellationToken)
        {
            // Get a slice of the receive buffer that is currently free
            Memory<byte> bufferFreeSpace = _receiveBuffer.Slice(_receiveBufferOffset);

            try
            {
                // Attempt to receive data asynchronously
                int result = await _socket.ReceiveAsync(bufferFreeSpace, SocketFlags.None, cancellationToken);
                return result;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                // If the socket would block, return 0 bytes read. The loop will retry.
                return 0;
            }
        }

        /// <summary>
        /// Processes the data currently held in the receive buffer, extracting and handling complete packets.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessReceivedData()
        {
            int processedPackets = 0;
            const int maxPacketsPerIteration = 10; // Process a limited number of packets per iteration to prevent starvation

            // Loop while there are packets to process and we haven't hit the iteration limit
            while (processedPackets < maxPacketsPerIteration && TryProcessNextPacket(out int consumedLength))
            {
                if (consumedLength == 0) // No complete packet found in this iteration
                    break;

                ShiftBuffer(consumedLength); // Shift remaining data to the beginning of the buffer
                processedPackets++;
            }
        }

        /// <summary>
        /// Shifts the remaining data in the receive buffer to the beginning after a packet has been consumed.
        /// </summary>
        /// <param name="consumedLength">The number of bytes consumed by the processed packet.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ShiftBuffer(int consumedLength)
        {
            int remainingLength = _receiveBufferOffset - consumedLength;
            if (remainingLength > 0)
            {
                // Copy remaining data to the start of the buffer
                _receiveBuffer.Slice(consumedLength, remainingLength).CopyTo(_receiveBuffer);
            }
            _receiveBufferOffset = remainingLength; // Update the offset for the next receive operation
        }
        #endregion

        #region Packet Processing
        /// <summary>
        /// Attempts to process the next complete packet from the receive buffer based on the client's current state.
        /// </summary>
        /// <param name="consumedLength">Output: The number of bytes consumed by the processed packet.</param>
        /// <returns>True if a packet was successfully processed, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryProcessNextPacket(out int consumedLength)
        {
            consumedLength = 0;
            // Get a span of the currently received data
            Span<byte> bufferSpan = _receiveBuffer.Span.Slice(0, _receiveBufferOffset);

            // Use a switch expression to dispatch to the appropriate packet processing method based on client state
            return State switch
            {
                ClientState.WaitingForDummyPacket => ProcessDummyPacket(bufferSpan, out consumedLength),
                ClientState.DhKeyExchange => ProcessDhKeyExchange(bufferSpan, out consumedLength),
                ClientState.Connected => ProcessGamePacket(bufferSpan, out consumedLength),
                _ => false // Should not happen in normal flow
            };
        }

        /// <summary>
        /// Processes the initial dummy packet received during connection establishment.
        /// </summary>
        /// <param name="buffer">The buffer containing received data.</param>
        /// <param name="consumedLength">Output: The number of bytes consumed by the dummy packet.</param>
        /// <returns>True if the dummy packet was successfully processed, false otherwise.</returns>
        private bool ProcessDummyPacket(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            return TryProcessSimplePacket(buffer, out consumedLength, () =>
            {
                // After processing dummy packet, transition to DH key exchange state
                UpdateStateAsync(ClientState.DhKeyExchange).GetAwaiter().GetResult();
                Log.Debug("Client {ClientId} processed dummy packet, transitioning to DH key exchange", ClientId);
            });
        }

        /// <summary>
        /// Processes the Diffie-Hellman key exchange packet.
        /// </summary>
        /// <param name="buffer">The buffer containing received data.</param>
        /// <param name="consumedLength">Output: The number of bytes consumed by the DH packet.</param>
        /// <returns>True if the DH packet was successfully processed, false otherwise.</returns>
        private bool ProcessDhKeyExchange(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            // Check for handshake timeout
            if (DateTime.UtcNow - _handshakeStartTime > HANDSHAKE_TIMEOUT)
            {
                Log.Warning("Client {ClientId} handshake timeout", ClientId);
                DisconnectAsync("Handshake timeout").GetAwaiter().GetResult();
                consumedLength = 0;
                return false;
            }

            // Handle the DH key packet (this method will update state to Connected upon success)
            HandleDhKeyPacket(buffer);
            consumedLength = buffer.Length; // Consume the entire buffer for DH packet
            return true;
        }

        /// <summary>
        /// Processes a regular game packet (which is encrypted).
        /// </summary>
        /// <param name="buffer">The buffer containing received data.</param>
        /// <param name="consumedLength">Output: The number of bytes consumed by the game packet.</param>
        /// <returns>True if the game packet was successfully processed, false otherwise.</returns>
        private bool ProcessGamePacket(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            return TryProcessEncryptedPacket(buffer, out consumedLength);
        }

        /// <summary>
        /// Attempts to process a simple (unencrypted) packet.
        /// </summary>
        /// <param name="buffer">The buffer containing received data.</param>
        /// <param name="consumedLength">Output: The number of bytes consumed by the packet.</param>
        /// <param name="onPacketProcessed">Action to execute after successful packet processing.</param>
        /// <returns>True if the packet was successfully processed, false otherwise.</returns>
        private bool TryProcessSimplePacket(ReadOnlySpan<byte> buffer, out int consumedLength, Action onPacketProcessed)
        {
            consumedLength = 0;
            // A simple packet must at least contain the length field
            if (buffer.Length < PACKET_LENGTH_SIZE)
                return false;

            // Read the declared length from the packet header
            short packetLength = BitConverter.ToInt16(buffer);

            // Validate the packet size against min/max limits
            if (!ValidatePacketSize(packetLength, "Dummy"))
            {
                DisconnectAsync("Invalid packet size").GetAwaiter().GetResult();
                return false;
            }

            // Check if the entire packet has been received yet
            if (buffer.Length < packetLength)
                return false;

            consumedLength = packetLength; // Set consumed length
            onPacketProcessed(); // Execute the callback
            return true;
        }

        /// <summary>
        /// Attempts to process an encrypted game packet. Handles partial decryption for length.
        /// </summary>
        /// <param name="buffer">The buffer containing received data.</param>
        /// <param name="consumedLength">Output: The number of bytes consumed by the packet.</param>
        /// <returns>True if the packet was successfully processed, false otherwise.</returns>
        private bool TryProcessEncryptedPacket(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            consumedLength = 0;

            // Step 1: Ensure we have enough data to decrypt the packet length (first 2 bytes)
            if (_decryptedBufferOffset < PACKET_LENGTH_SIZE)
            {
                int lengthBytesNeeded = PACKET_LENGTH_SIZE - _decryptedBufferOffset;
                if (buffer.Length < lengthBytesNeeded)
                    return false; // Not enough data to decrypt length yet

                // Decrypt the length bytes directly into the decrypted buffer
                _cryptographer.Decrypt(
                    buffer.Slice(0, lengthBytesNeeded),
                    _decryptedBuffer.Span.Slice(_decryptedBufferOffset, lengthBytesNeeded)
                );
                _decryptedBufferOffset += lengthBytesNeeded;
                consumedLength += lengthBytesNeeded;
            }

            // Step 2: Read the declared total length of the packet from the decrypted header
            short declaredLength = BitConverter.ToInt16(_decryptedBuffer.Span);
            int totalPacketSize = declaredLength + PACKET_SIGNATURE_SIZE;

            // Validate the total packet size
            if (!ValidatePacketSize(totalPacketSize, "Game"))
            {
                _decryptedBufferOffset = 0; // Reset offset on invalid packet
                DisconnectAsync("Invalid packet size").GetAwaiter().GetResult();
                return false;
            }

            // Step 3: Check if the full encrypted packet (including signature) has been received
            int remainingEncryptedBytes = totalPacketSize - _decryptedBufferOffset;
            if (buffer.Length - consumedLength < remainingEncryptedBytes)
                return false; // Full packet not yet received

            // Step 4: Process the complete packet (decrypt remaining data and enqueue)
            ProcessPacket(buffer.Slice(consumedLength), remainingEncryptedBytes, totalPacketSize);
            consumedLength += remainingEncryptedBytes; // Update total consumed length

            return true;
        }

        /// <summary>
        /// Decrypts the full packet data and enqueues it for further processing by the PacketHandler.
        /// </summary>
        /// <param name="buffer">The remaining encrypted buffer slice.</param>
        /// <param name="bytesToDecrypt">The number of bytes to decrypt from the buffer.</param>
        /// <param name="totalPacketSize">The total size of the complete packet (including signature).</param>
        private void ProcessPacket(ReadOnlySpan<byte> buffer, int bytesToDecrypt, int totalPacketSize)
        {
            // Decrypt the remaining portion of the packet into the decrypted buffer
            _cryptographer.Decrypt(
                buffer.Slice(0, bytesToDecrypt),
                _decryptedBuffer.Span.Slice(_decryptedBufferOffset, bytesToDecrypt)
            );

            // Create a Packet object from the complete decrypted data
            // Use ToArray() to create a copy, as the _decryptedBuffer is reused
            using Packet packet = new Packet(_decryptedBuffer.Slice(0, totalPacketSize).ToArray());
            _decryptedBufferOffset = 0; // Reset decrypted buffer offset for the next packet

            // Validate the packet's completeness and type (client vs. server)
            if (packet.IsComplete && packet.IsClientPacket())
            {
                // Try to write the client message to the message channel
                if (!_messageWriter.TryWrite(new ClientMessage(this, packet)))
                {
                    Log.Warning("Failed to queue packet from client {ClientId} - message queue full", ClientId);
                }
                else
                {
                    Interlocked.Increment(ref _packetsReceived); // Increment received packet count
                }
            }
            else
            {
                Log.Warning("Failed to queue packet from client {ClientId} - message not complete or not a client packet", ClientId);
            }
        }

        /// <summary>
        /// Validates the declared length of an incoming packet against predefined min/max sizes.
        /// </summary>
        /// <param name="packetLength">The declared length of the packet.</param>
        /// <param name="packetType">A string indicating the type of packet (e.g., "Dummy", "Game") for logging.</param>
        /// <returns>True if the packet size is valid, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ValidatePacketSize(int packetLength, string packetType)
        {
            if (packetLength < MIN_PACKET_SIZE)
            {
                Log.Warning("Client {ClientId} sent packet too small ({PacketType}: {PacketLength} < {MinSize})",
                    ClientId, packetType, packetLength, MIN_PACKET_SIZE);
                return false;
            }

            if (packetLength > MAX_PACKET_SIZE)
            {
                Log.Warning("Client {ClientId} sent oversized packet ({PacketType}: {PacketLength} > {MaxSize})",
                    ClientId, packetType, packetLength, MAX_PACKET_SIZE);
                return false;
            }

            return true;
        }
        #endregion

        #region Diffie-Hellman Key Exchange
        /// <summary>
        /// Handles the incoming Diffie-Hellman key exchange response from the client.
        /// </summary>
        /// <param name="dhKeyBuffer">The buffer containing the encrypted DH key data.</param>
        private void HandleDhKeyPacket(ReadOnlySpan<byte> dhKeyBuffer)
        {
            try
            {
                // Rent a temporary buffer for decryption
                byte[] decryptedBufferTemp = ArrayPool<byte>.Shared.Rent(dhKeyBuffer.Length);
                try
                {
                    // Decrypt the incoming DH key data
                    _cryptographer.Decrypt(dhKeyBuffer, decryptedBufferTemp.AsSpan(0, dhKeyBuffer.Length));

                    // Create a Packet from the decrypted data
                    using Packet packet = new Packet(decryptedBufferTemp.AsSpan(0, dhKeyBuffer.Length));

                    // Try to extract the client's public key from the packet
                    if (packet.TryExtractDHKey(out string clientPublicKey))
                    {
                        CompleteDhKeyExchange(clientPublicKey); // Complete the DH handshake
                    }
                    else
                    {
                        Log.Warning("Client {ClientId} sent invalid DH key packet", ClientId);
                        DisconnectAsync("Invalid DH key").GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    // Return the rented buffer
                    ArrayPool<byte>.Shared.Return(decryptedBufferTemp);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing DH key packet for client {ClientId}", ClientId);
                DisconnectAsync("DH key processing error").GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Completes the Diffie-Hellman key exchange by deriving the final encryption key.
        /// </summary>
        /// <param name="clientPublicKey">The client's public key as a hex string.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CompleteDhKeyExchange(string clientPublicKey)
        {
            _dhKeyExchange.HandleClientResponse(clientPublicKey); // Process client's public key
            byte[] finalKey = _dhKeyExchange.DeriveEncryptionKey(); // Derive the shared secret key
            _cryptographer.GenerateKey(finalKey); // Initialize the cryptographer with the new key
            _cryptographer.Reset(); // Reset cryptographer state

            UpdateStateAsync(ClientState.Connected).GetAwaiter().GetResult(); // Transition client state to Connected
            Log.Information("DH key exchange completed successfully for client {ClientId}", ClientId);
        }
        #endregion

        #region Connection Health Monitoring
        /// <summary>
        /// Monitors the client's connection health, checking for idle timeouts and handshake timeouts.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        private async Task MonitorConnectionHealthAsync(CancellationToken cancellationToken)
        {
            // Use PeriodicTimer for efficient, timer-based checks
            using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    // Check for idle timeout
                    if (DateTime.UtcNow - LastActivityTime > IDLE_TIMEOUT)
                    {
                        Log.Information("Client {ClientId} idle timeout", ClientId);
                        await DisconnectAsync("Idle timeout");
                        break; // Exit loop after disconnecting
                    }

                    // Check for handshake timeout if still in a pre-connected state
                    if (State != ClientState.Connected &&
                        DateTime.UtcNow - _handshakeStartTime > HANDSHAKE_TIMEOUT)
                    {
                        Log.Warning("Client {ClientId} handshake timeout", ClientId);
                        await DisconnectAsync("Handshake timeout");
                        break; // Exit loop after disconnecting
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in connection health monitor for client {ClientId}", ClientId);
                }
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Determines if an exception should lead to a client disconnection.
        /// Non-blocking socket errors (WouldBlock) are typically not fatal.
        /// </summary>
        /// <param name="ex">The exception to evaluate.</param>
        /// <returns>True if the exception is considered fatal and should cause a disconnect, false otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldDisconnectOnError(Exception ex)
        {
            return ex switch
            {
                SocketException se => se.SocketErrorCode switch
                {
                    SocketError.WouldBlock => false, // Non-blocking operation would block, not an error
                    SocketError.IOPending => false,  // Asynchronous operation is still pending
                    SocketError.NoBufferSpaceAvailable => false, // Temporary network congestion
                    SocketError.Interrupted => false, // Operation was interrupted (e.g., by system call)
                    _ => true // Other socket errors are generally fatal
                },
                ObjectDisposedException => true, // Object (e.g., socket) was already disposed
                _ => true // All other exceptions are considered fatal
            };
        }

        /// <summary>
        /// Updates the timestamp of the client's last activity.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateActivity()
        {
            LastActivityTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Increments the count of consecutive errors.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementErrorCount()
        {
            Interlocked.Increment(ref _consecutiveErrors);
        }

        /// <summary>
        /// Resets the count of consecutive errors to zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetErrorCount()
        {
            Interlocked.Exchange(ref _consecutiveErrors, 0);
        }

        /// <summary>
        /// Updates the client's state in a thread-safe manner.
        /// </summary>
        /// <param name="newState">The new state to set.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task UpdateStateAsync(ClientState newState)
        {
            // Use a semaphore to ensure state changes are atomic and thread-safe
            await _stateLock.WaitAsync();
            try
            {
                State = newState;
            }
            finally
            {
                _stateLock.Release(); // Release the semaphore
            }
        }
        #endregion
    }
}
