

using Microsoft.Extensions.Logging;
using MMORPGServer.Infrastructure.Security;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
namespace MMORPGServer.Infrastructure.Networking.Clients
{

    public sealed class GameClient : IGameClient
    {
        #region Constants
        private const int MAX_PACKET_SIZE = 1024;
        private const int BUFFER_SIZE = 8192;
        private const int PACKET_LENGTH_SIZE = 2;
        private const int PACKET_SIGNATURE_SIZE = 8;
        private const int MIN_PACKET_SIZE = PACKET_LENGTH_SIZE + PACKET_SIGNATURE_SIZE;

        // Rate limiting
        private const int MAX_PACKETS_PER_SECOND = 100;
        private const int MAX_BYTES_PER_SECOND = 100_000;
        private const int RATE_LIMIT_WINDOW_SECONDS = 1;
        private const int MAX_CONSECUTIVE_ERRORS = 5;

        // Timeouts
        private static readonly TimeSpan HANDSHAKE_TIMEOUT = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan IDLE_TIMEOUT = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan WOULD_BLOCK_RETRY_DELAY = TimeSpan.FromMilliseconds(10);
        #endregion

        #region Properties
        public uint ClientId { get; }
        public Player Player { get; set; }
        public bool IsConnected => State != ClientState.Disconnected && _tcpClient?.Connected == true;
        public string IPAddress { get; }
        public DateTime ConnectedAt { get; }
        public ClientState State { get; private set; }
        public DateTime LastActivityTime { get; private set; }
        #endregion

        #region Private Fields
        private readonly TcpClient _tcpClient;
        private readonly Socket _socket;
        private readonly ILogger<GameClient> _logger;
        private readonly DiffieHellmanKeyExchange _dhKeyExchange;
        private readonly TQCast5Cryptographer _cryptographer;
        private readonly ChannelWriter<ClientMessage> _messageWriter;

        private readonly Channel<ReadOnlyMemory<byte>> _sendChannel;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        private readonly Memory<byte> _receiveBuffer = new byte[BUFFER_SIZE];
        private int _receiveBufferOffset = 0;

        // For encrypted packet processing - tracks decrypted data
        private readonly Memory<byte> _decryptedBuffer = new byte[MAX_PACKET_SIZE];
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

        // Rate limiting
        private readonly RateLimiter _packetRateLimiter;
        private readonly RateLimiter _byteRateLimiter;

        // Security
        private int _consecutiveErrors;
        private DateTime _handshakeStartTime;
        private readonly HashSet<GamePackets> _recentPacketTypes = new();
        private DateTime _lastPacketTypeReset = DateTime.UtcNow;

        // Anti-flood
        private readonly Queue<DateTime> _recentPacketTimes = new();
        private const int FLOOD_DETECTION_WINDOW_MS = 100;
        private const int FLOOD_DETECTION_THRESHOLD = 10;


        #endregion

        #region Constructor
        public GameClient(
            uint clientId,
            TcpClient tcpClient,
            DiffieHellmanKeyExchange dhKeyExchange,
            TQCast5Cryptographer cryptographer,
            ChannelWriter<ClientMessage> messageWriter,
            ILogger<GameClient> logger)
        {
            ClientId = clientId;
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _socket = tcpClient.Client;
            _dhKeyExchange = dhKeyExchange ?? throw new ArgumentNullException(nameof(dhKeyExchange));
            _cryptographer = cryptographer ?? throw new ArgumentNullException(nameof(cryptographer));
            _messageWriter = messageWriter ?? throw new ArgumentNullException(nameof(messageWriter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            IPAddress = tcpClient.Client.RemoteEndPoint?.ToString();
            ConnectedAt = DateTime.UtcNow;
            LastActivityTime = DateTime.UtcNow;
            State = ClientState.Connecting;

            // Configure send channel with bounded capacity to prevent memory issues
            _sendChannel = Channel.CreateBounded<ReadOnlyMemory<byte>>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            // Initialize rate limiters
            _packetRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = MAX_PACKETS_PER_SECOND,
                ReplenishmentPeriod = TimeSpan.FromSeconds(RATE_LIMIT_WINDOW_SECONDS),
                TokensPerPeriod = MAX_PACKETS_PER_SECOND,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });

            _byteRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = MAX_BYTES_PER_SECOND,
                ReplenishmentPeriod = TimeSpan.FromSeconds(RATE_LIMIT_WINDOW_SECONDS),
                TokensPerPeriod = MAX_BYTES_PER_SECOND,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });

            ConfigureSocket();
            _connectionTimer.Start();
            // Player will be spawned after connection
        }

        private void ConfigureSocket()
        {
            try
            {
                _socket.NoDelay = true;
                _socket.SendBufferSize = BUFFER_SIZE;
                _socket.ReceiveBufferSize = BUFFER_SIZE;
                _socket.LingerState = new LingerOption(false, 0);
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                // Set non-blocking mode for better control
                _socket.Blocking = false;

                // Set send/receive timeouts
                _socket.SendTimeout = 5000;
                _socket.ReceiveTimeout = 5000;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure socket options for client {ClientId}", ClientId);
            }
        }
        #endregion

        #region Public Methods
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            var processTasks = new[]
            {
                ProcessIncomingDataAsync(linkedCts.Token),
                ProcessOutgoingPacketsAsync(linkedCts.Token),
                MonitorConnectionHealthAsync(linkedCts.Token)
            };

            try
            {
                _handshakeStartTime = DateTime.UtcNow;
                await InitializeConnectionAsync();
                await Task.WhenAny(processTasks);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Client {ClientId} operation cancelled", ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error in client {ClientId}", ClientId);
            }
            finally
            {
                await DisconnectAsync("Connection terminated");
            }
        }

        public async ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData)
        {
            if (_disposed || State == ClientState.Disconnected)
                return;

            if (packetData.Length > MAX_PACKET_SIZE)
            {
                _logger.LogError("Attempted to send packet larger than MAX_PACKET_SIZE ({PacketLength} > {MaxPacketSize})",
                    packetData.Length, MAX_PACKET_SIZE);
                return;
            }

            try
            {
                // Check rate limit for outgoing packets
                using var lease = await _byteRateLimiter.AcquireAsync(packetData.Length, _cancellationTokenSource.Token);
                if (!lease.IsAcquired)
                {
                    _logger.LogWarning("Client {ClientId} exceeded outgoing byte rate limit", ClientId);
                    return;
                }

                await _sendChannel.Writer.WriteAsync(packetData, _cancellationTokenSource.Token);
                Interlocked.Increment(ref _packetsSent);
            }
            catch (ChannelClosedException)
            {
                // Channel is closed, client is disconnecting
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue packet for client {ClientId}", ClientId);
            }
        }

        public async ValueTask DisconnectAsync(string reason = "")
        {
            await _stateLock.WaitAsync();
            try
            {
                if (State == ClientState.Disconnected)
                    return;

                State = ClientState.Disconnected;
                _connectionTimer.Stop();

                _logger.LogInformation("Disconnecting client {ClientId}: {Reason} (Duration: {Duration}, Packets R/S: {Received}/{Sent}, Bytes R/S: {BytesReceived}/{BytesSent})",
                    ClientId, reason, _connectionTimer.Elapsed, _packetsReceived, _packetsSent, _bytesReceived, _bytesSent);

                _cancellationTokenSource.Cancel();
                _sendChannel.Writer.TryComplete();

                try
                {
                    _tcpClient?.Close();
                }
                catch { }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (State != ClientState.Disconnected)
            {
                DisconnectAsync("Disposed").GetAwaiter().GetResult();
            }

            _packetRateLimiter?.Dispose();
            _byteRateLimiter?.Dispose();
            _cancellationTokenSource?.Dispose();
            _stateLock?.Dispose();
            _tcpClient?.Dispose();
        }

        public async Task ConnectPlayer()
        {
            Player = new Player(this);
        }
        #endregion

        #region Connection Initialization
        private async Task InitializeConnectionAsync()
        {
            await SendDHKeyExchangeAsync();
            await UpdateStateAsync(ClientState.WaitingForDummyPacket);
        }

        private async Task SendDHKeyExchangeAsync()
        {
            try
            {
                var defaultKey = Encoding.ASCII.GetBytes("R3Xx97ra5j8D6uZz");
                _cryptographer.GenerateKey(defaultKey);

                var memory = _dhKeyExchange.CreateKeyExchangePacket();
                var encryptedPacket = ArrayPool<byte>.Shared.Rent(memory.Length);

                try
                {
                    _cryptographer.Encrypt(memory.Span, encryptedPacket.AsSpan(0, memory.Length));
                    await SendDataWithRetryAsync(new ReadOnlyMemory<byte>(encryptedPacket, 0, memory.Length));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(encryptedPacket);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send DH key exchange for client {ClientId}", ClientId);
                throw;
            }
        }
        #endregion

        #region Outgoing Data Processing
        private async Task ProcessOutgoingPacketsAsync(CancellationToken cancellationToken)
        {
            await foreach (var packetMemory in _sendChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var dataToSend = PrepareOutgoingData(packetMemory);
                    await SendDataWithRetryAsync(dataToSend, cancellationToken);

                    Interlocked.Add(ref _bytesSent, dataToSend.Length);
                    UpdateActivity();
                }
                catch (Exception ex) when (!ShouldDisconnectOnError(ex))
                {
                    _logger.LogDebug("Non-fatal send error for client {ClientId}: {Error}", ClientId, ex.Message);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal send error for client {ClientId}", ClientId);
                    await DisconnectAsync($"Send error: {ex.GetType().Name}");
                    break;
                }
            }
        }

        private async Task SendDataWithRetryAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            var retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    await _socket.SendAsync(data, SocketFlags.None, cancellationToken);
                    return;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;

                    _logger.LogDebug("Socket would block on send for client {ClientId}, retry {Retry}/{MaxRetries}",
                        ClientId, retryCount, maxRetries);

                    await Task.Delay(WOULD_BLOCK_RETRY_DELAY * retryCount, cancellationToken);
                }
            }
        }

        private ReadOnlyMemory<byte> PrepareOutgoingData(ReadOnlyMemory<byte> packetData)
        {
            if (State == ClientState.Connected && _cryptographer.IsInitialized)
            {
                var encryptedMemory = ArrayPool<byte>.Shared.Rent(packetData.Length);
                try
                {
                    _cryptographer.Encrypt(packetData.Span, encryptedMemory.AsSpan(0, packetData.Length));
                    // Return a copy since we're returning the rented array
                    return new ReadOnlyMemory<byte>(encryptedMemory.AsSpan(0, packetData.Length).ToArray());
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(encryptedMemory);
                }
            }

            return packetData;
        }
        #endregion

        #region Incoming Data Processing
        private async Task ProcessIncomingDataAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var bytesRead = await ReceiveDataWithRetryAsync(cancellationToken);
                    if (bytesRead == 0)
                    {
                        _logger.LogDebug("Client {ClientId} closed connection gracefully", ClientId);
                        break;
                    }

                    _receiveBufferOffset += bytesRead;
                    Interlocked.Add(ref _bytesReceived, bytesRead);
                    UpdateActivity();

                    // Check byte rate limit
                    using var lease = await _byteRateLimiter.AcquireAsync(bytesRead, cancellationToken);
                    if (!lease.IsAcquired)
                    {
                        _logger.LogWarning("Client {ClientId} exceeded incoming byte rate limit", ClientId);
                        await DisconnectAsync("Byte rate limit exceeded");
                        break;
                    }

                    ProcessReceivedData();
                    ResetErrorCount();
                }
                catch (Exception ex) when (!ShouldDisconnectOnError(ex))
                {
                    IncrementErrorCount();
                    _logger.LogDebug("Non-fatal receive error for client {ClientId}: {Error}", ClientId, ex.Message);

                    if (_consecutiveErrors >= MAX_CONSECUTIVE_ERRORS)
                    {
                        await DisconnectAsync("Too many consecutive errors");
                        break;
                    }

                    await Task.Delay(WOULD_BLOCK_RETRY_DELAY, cancellationToken);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fatal receive error for client {ClientId}", ClientId);
                    await DisconnectAsync($"Receive error: {ex.GetType().Name}");
                    break;
                }
            }
        }

        private async Task<int> ReceiveDataWithRetryAsync(CancellationToken cancellationToken)
        {
            var bufferFreeSpace = _receiveBuffer.Slice(_receiveBufferOffset);

            try
            {
                var result = await _socket.ReceiveAsync(bufferFreeSpace, SocketFlags.None, cancellationToken);
                return result;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                // No data available right now, this is normal for non-blocking sockets
                return 0;
            }
        }

        private void ProcessReceivedData()
        {
            var processedPackets = 0;
            const int maxPacketsPerIteration = 10; // Prevent starvation

            while (processedPackets < maxPacketsPerIteration && TryProcessNextPacket(out var consumedLength))
            {
                if (consumedLength == 0)
                    break;

                ShiftBuffer(consumedLength);
                processedPackets++;
            }
        }

        private void ShiftBuffer(int consumedLength)
        {
            var remainingLength = _receiveBufferOffset - consumedLength;
            if (remainingLength > 0)
            {
                _receiveBuffer.Slice(consumedLength, remainingLength).CopyTo(_receiveBuffer);
            }
            _receiveBufferOffset = remainingLength;
        }
        #endregion

        #region Packet Processing
        private bool TryProcessNextPacket(out int consumedLength)
        {
            consumedLength = 0;
            var bufferSpan = _receiveBuffer.Span.Slice(0, _receiveBufferOffset);

            // Check flood detection
            if (IsFlooding())
            {
                _logger.LogWarning("Client {ClientId} is flooding, disconnecting", ClientId);
                DisconnectAsync("Flood detected").GetAwaiter().GetResult();
                return false;
            }

            return State switch
            {
                ClientState.WaitingForDummyPacket => ProcessDummyPacket(bufferSpan, out consumedLength),
                ClientState.DhKeyExchange => ProcessDhKeyExchange(bufferSpan, out consumedLength),
                ClientState.Connected => ProcessGamePacket(bufferSpan, out consumedLength),
                _ => false
            };
        }

        private bool ProcessDummyPacket(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            return TryProcessSimplePacket(buffer, out consumedLength, () =>
            {
                UpdateStateAsync(ClientState.DhKeyExchange).GetAwaiter().GetResult();
                _logger.LogDebug("Client {ClientId} processed dummy packet, transitioning to DH key exchange", ClientId);
            });
        }

        private bool ProcessDhKeyExchange(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            // Check handshake timeout
            if (DateTime.UtcNow - _handshakeStartTime > HANDSHAKE_TIMEOUT)
            {
                _logger.LogWarning("Client {ClientId} handshake timeout", ClientId);
                DisconnectAsync("Handshake timeout").GetAwaiter().GetResult();
                consumedLength = 0;
                return false;
            }

            HandleDhKeyPacket(buffer);
            consumedLength = buffer.Length;
            return true;
        }

        private bool ProcessGamePacket(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            return TryProcessEncryptedPacket(buffer, out consumedLength);
        }

        private bool TryProcessSimplePacket(ReadOnlySpan<byte> buffer, out int consumedLength, Action onPacketProcessed)
        {
            consumedLength = 0;
            if (buffer.Length < PACKET_LENGTH_SIZE)
                return false;

            var packetLength = BitConverter.ToUInt16(buffer);

            if (!ValidatePacketSize(packetLength, "Dummy"))
            {
                DisconnectAsync("Invalid packet size").GetAwaiter().GetResult();
                return false;
            }

            if (buffer.Length < packetLength)
                return false;

            consumedLength = packetLength;
            onPacketProcessed();
            return true;
        }

        private bool TryProcessEncryptedPacket(ReadOnlySpan<byte> buffer, out int consumedLength)
        {
            consumedLength = 0;

            // Handle partial length decryption
            if (_decryptedBufferOffset < PACKET_LENGTH_SIZE)
            {
                var lengthBytesNeeded = PACKET_LENGTH_SIZE - _decryptedBufferOffset;
                if (buffer.Length < lengthBytesNeeded)
                    return false;

                // Decrypt missing length bytes
                _cryptographer.Decrypt(
                    buffer.Slice(0, lengthBytesNeeded),
                    _decryptedBuffer.Span.Slice(_decryptedBufferOffset, lengthBytesNeeded)
                );
                _decryptedBufferOffset += lengthBytesNeeded;
                consumedLength += lengthBytesNeeded;
            }

            // Now we have the full length
            var declaredLength = BitConverter.ToUInt16(_decryptedBuffer.Span);
            var totalPacketSize = declaredLength + PACKET_SIGNATURE_SIZE;

            if (!ValidatePacketSize(totalPacketSize, "Game"))
            {
                _decryptedBufferOffset = 0; // Reset for next packet
                DisconnectAsync("Invalid packet size").GetAwaiter().GetResult();
                return false;
            }

            // Check if we have enough data for the full packet
            var remainingEncryptedBytes = totalPacketSize - _decryptedBufferOffset;
            if (buffer.Length - consumedLength < remainingEncryptedBytes)
                return false;

            // Decrypt the rest of the packet
            ProcessPacket(buffer.Slice(consumedLength), remainingEncryptedBytes, totalPacketSize);
            consumedLength += remainingEncryptedBytes;

            return true;
        }

        private void ProcessPacket(ReadOnlySpan<byte> buffer, int bytesToDecrypt, int totalPacketSize)
        {
            // Check packet rate limit
            using var lease = _packetRateLimiter.AcquireAsync(1).GetAwaiter().GetResult();
            if (!lease.IsAcquired)
            {
                _logger.LogWarning("Client {ClientId} exceeded packet rate limit", ClientId);
                _decryptedBufferOffset = 0;
                DisconnectAsync("Packet rate limit exceeded").GetAwaiter().GetResult();
                return;
            }
            //

            // Decrypt remaining bytes
            _cryptographer.Decrypt(
                buffer.Slice(0, bytesToDecrypt),
                _decryptedBuffer.Span.Slice(_decryptedBufferOffset, bytesToDecrypt)
            );

            // Create packet from complete decrypted data
            var packetData = _decryptedBuffer.Slice(0, totalPacketSize).ToArray();

            // Reset for next packet
            _decryptedBufferOffset = 0;

            // Send to processing pipeline
            using var packet = new Packet(packetData);

            // Track packet type diversity (security check)
            TrackPacketType(packet.Type);
            if (packet.IsComplete && packet.IsClientPacket())
            {

                if (!_messageWriter.TryWrite(new ClientMessage(ClientId, packet)))
                {
                    _logger.LogWarning("Failed to queue packet from client {ClientId} - message queue full", ClientId);
                }
                else
                {
                    Interlocked.Increment(ref _packetsReceived);
                    RecordPacketTime();
                }
            }
            else
            {
                _logger.LogWarning("Failed to queue packet from client {ClientId} - message not complete", ClientId);
            }
        }

        private bool ValidatePacketSize(int packetLength, string packetType)
        {
            if (packetLength < MIN_PACKET_SIZE)
            {
                _logger.LogWarning("Client {ClientId} sent packet too small ({PacketType}: {PacketLength} < {MinSize})",
                    ClientId, packetType, packetLength, MIN_PACKET_SIZE);
                return false;
            }

            if (packetLength > MAX_PACKET_SIZE)
            {
                _logger.LogWarning("Client {ClientId} sent oversized packet ({PacketType}: {PacketLength} > {MaxSize})",
                    ClientId, packetType, packetLength, MAX_PACKET_SIZE);
                return false;
            }

            return true;
        }
        #endregion

        #region Diffie-Hellman Key Exchange
        private void HandleDhKeyPacket(ReadOnlySpan<byte> dhKeyBuffer)
        {
            try
            {
                var decryptedBuffer = ArrayPool<byte>.Shared.Rent(dhKeyBuffer.Length);
                try
                {
                    _cryptographer.Decrypt(dhKeyBuffer, decryptedBuffer.AsSpan(0, dhKeyBuffer.Length));

                    using var packet = new Packet(decryptedBuffer.AsSpan(0, dhKeyBuffer.Length));

                    if (packet.TryExtractDHKey(out var clientPublicKey))
                    {
                        CompleteDhKeyExchange(clientPublicKey);
                    }
                    else
                    {
                        _logger.LogWarning("Client {ClientId} sent invalid DH key packet", ClientId);
                        DisconnectAsync("Invalid DH key").GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(decryptedBuffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DH key packet for client {ClientId}", ClientId);
                DisconnectAsync("DH key processing error").GetAwaiter().GetResult();
            }
        }

        private void CompleteDhKeyExchange(string clientPublicKey)
        {
            _dhKeyExchange.HandleClientResponse(clientPublicKey);
            var finalKey = _dhKeyExchange.DeriveEncryptionKey();
            _cryptographer.GenerateKey(finalKey);
            _cryptographer.Reset();

            UpdateStateAsync(ClientState.Connected).GetAwaiter().GetResult();
            _logger.LogInformation("DH key exchange completed successfully for client {ClientId}", ClientId);
        }
        #endregion

        #region Connection Health Monitoring
        private async Task MonitorConnectionHealthAsync(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    // Check idle timeout
                    if (DateTime.UtcNow - LastActivityTime > IDLE_TIMEOUT)
                    {
                        _logger.LogInformation("Client {ClientId} idle timeout", ClientId);
                        await DisconnectAsync("Idle timeout");
                        break;
                    }

                    // Check handshake timeout for non-connected clients
                    if (State != ClientState.Connected &&
                        DateTime.UtcNow - _handshakeStartTime > HANDSHAKE_TIMEOUT)
                    {
                        _logger.LogWarning("Client {ClientId} handshake timeout", ClientId);
                        await DisconnectAsync("Handshake timeout");
                        break;
                    }

                    // Reset packet type tracking periodically
                    if (DateTime.UtcNow - _lastPacketTypeReset > TimeSpan.FromMinutes(1))
                    {
                        _recentPacketTypes.Clear();
                        _lastPacketTypeReset = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in connection health monitor for client {ClientId}", ClientId);
                }
            }
        }
        #endregion

        #region Security & Helper Methods
        private bool ShouldDisconnectOnError(Exception ex)
        {
            return ex switch
            {
                SocketException se => se.SocketErrorCode switch
                {
                    SocketError.WouldBlock => false,
                    SocketError.IOPending => false,
                    SocketError.NoBufferSpaceAvailable => false,
                    SocketError.Interrupted => false,
                    _ => true
                },
                ObjectDisposedException => true,
                _ => true
            };
        }

        private void UpdateActivity()
        {
            LastActivityTime = DateTime.UtcNow;
        }

        private void IncrementErrorCount()
        {
            Interlocked.Increment(ref _consecutiveErrors);
        }

        private void ResetErrorCount()
        {
            Interlocked.Exchange(ref _consecutiveErrors, 0);
        }

        private async Task UpdateStateAsync(ClientState newState)
        {
            await _stateLock.WaitAsync();
            try
            {
                State = newState;
            }
            finally
            {
                _stateLock.Release();
            }
        }

        private void TrackPacketType(GamePackets packetType)
        {
            lock (_recentPacketTypes)
            {
                _recentPacketTypes.Add(packetType);

                // Security check: too many different packet types might indicate fuzzing
                if (_recentPacketTypes.Count > 50)
                {
                    _logger.LogWarning("Client {ClientId} sending too many different packet types ({Count})",
                        ClientId, _recentPacketTypes.Count);
                }
            }
        }

        private void RecordPacketTime()
        {
            var now = DateTime.UtcNow;
            lock (_recentPacketTimes)
            {
                _recentPacketTimes.Enqueue(now);

                // Remove old entries
                while (_recentPacketTimes.Count > 0 &&
                       (now - _recentPacketTimes.Peek()).TotalMilliseconds > FLOOD_DETECTION_WINDOW_MS)
                {
                    _recentPacketTimes.Dequeue();
                }
            }
        }

        private bool IsFlooding()
        {
            lock (_recentPacketTimes)
            {
                return _recentPacketTimes.Count > FLOOD_DETECTION_THRESHOLD;
            }
        }
        #endregion
    }
}