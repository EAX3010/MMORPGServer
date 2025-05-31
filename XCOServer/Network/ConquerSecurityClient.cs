public sealed class ConquerSecurityClient : IGameClient
{
    public uint ClientId { get; }
    public IPlayer? Player { get; set; }
    public bool IsConnected => !_disposed && _tcpClient.Connected;
    public string? IPAddress { get; }
    public DateTime ConnectedAt { get; }

    private readonly TcpClient _tcpClient;
    private readonly Socket _socket;
    private readonly ILogger<ConquerSecurityClient> _logger;
    private readonly IDHKeyExchange _dhKeyExchange;
    private readonly ICryptographer _cryptographer;
    private readonly ChannelWriter<ClientMessage> _messageWriter;

    // Send buffer and state management
    private readonly byte[] _sendBuffer = new byte[8192];
    private readonly object _sendLock = new object();
    private bool _isSending = false;
    private readonly Queue<byte[]> _sendQueue = new();

    private bool _disposed;
    private bool _dhKeySet;
    private bool _firstPacketReceived;
    private readonly byte[] _dhKeyBuffer = new byte[4096];
    private int _dhKeyBufferLength;

    // New fields to match SecuritySocket behavior
    private readonly byte[] _recvBuffer = new byte[1024];
    private int _bytesRecv = 0;
    private int _decryptCount = 0;
    private bool _receivingPacketData = false;
    private ushort _expectedPacketLength = 0;

    public ConquerSecurityClient(
        uint clientId,
        TcpClient tcpClient,
        IDHKeyExchange dhKeyExchange,
        ICryptographer cryptographer,
        ChannelWriter<ClientMessage> messageWriter,
        ILogger<ConquerSecurityClient> logger)
    {
        ClientId = clientId;
        _tcpClient = tcpClient;
        _socket = tcpClient.Client;
        _dhKeyExchange = dhKeyExchange;
        _cryptographer = cryptographer;
        _messageWriter = messageWriter;
        _logger = logger;
        IPAddress = tcpClient.Client.RemoteEndPoint?.ToString();
        ConnectedAt = DateTime.UtcNow;

        // Configure socket for optimal performance
        _socket.NoDelay = true;
        _socket.SendBufferSize = 8192;
        _socket.ReceiveBufferSize = 8192;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting client {ClientId} connection process", ClientId);

            await SendDHKeyExchangeAsync();
            await ProcessPacketsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Client {ClientId} cancelled", ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in client {ClientId} processing", ClientId);
        }
        finally
        {
            await DisconnectAsync("Connection terminated");
        }
    }

    private async Task SendDHKeyExchangeAsync()
    {
        try
        {
            _logger.LogDebug("Initializing cryptography for client {ClientId}", ClientId);

            var defaultKey = Encoding.ASCII.GetBytes("R3Xx97ra5j8D6uZz");
            _cryptographer.GenerateKey(defaultKey);

            var dhKeyPacket = CreateDHKeyPacket();

            _logger.LogDebug("Encrypting and sending DH key exchange packet ({Length} bytes) to client {ClientId}",
                dhKeyPacket.Length, ClientId);

            // ENCRYPT the DH key packet before sending
            var encryptedPacket = new byte[dhKeyPacket.Length];
            _cryptographer.Encrypt(dhKeyPacket, encryptedPacket);

            await SendDataAsync(encryptedPacket);

            _logger.LogInformation("Encrypted DH key exchange sent to client {ClientId}", ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send DH key exchange to client {ClientId}", ClientId);
            throw;
        }
    }

    private byte[] CreateDHKeyPacket()
    {
        var publicKey = _dhKeyExchange.GenerateRequest();
        var publicKeyBytes = Encoding.ASCII.GetBytes(publicKey);
        var packet = new ConquerPacket(0, true);
        var pBytes = DiffieHellmanKeyExchange.KeyExchange.GetP();
        var gBytes = DiffieHellmanKeyExchange.KeyExchange.GetG();
        uint size = (uint)(75 + pBytes.Length + gBytes.Length + publicKey.Length);
        packet.Seek(11);
        packet.WriteUInt32(size - 11);
        packet.WriteUInt32(10);
        packet.SeekForward(10);
        packet.WriteUInt32(8);
        packet.SeekForward(8);
        packet.WriteUInt32(8);
        packet.SeekForward(8);
        packet.WriteUInt32((uint)pBytes.Length);
        packet.WriteBytes(pBytes);
        packet.WriteUInt32((uint)gBytes.Length);
        packet.WriteBytes(gBytes);
        packet.WriteUInt32((uint)publicKeyBytes.Length);
        packet.WriteBytes(publicKeyBytes);
        packet.SeekForward(2);
        packet.WriteSeal(true);
        return packet.ToMemory().ToArray();
    }

    private async Task SendDataAsync(byte[] data)
    {
        if (_disposed || !IsConnected)
        {
            _logger.LogWarning("Attempted to send data to disconnected client {ClientId}", ClientId);
            return;
        }

        bool shouldSend = false;
        lock (_sendLock)
        {
            if (_isSending)
            {
                _sendQueue.Enqueue(data);
                _logger.LogTrace("Queued {ByteCount} bytes for client {ClientId} (queue size: {QueueSize})",
                    data.Length, ClientId, _sendQueue.Count);
                return;
            }
            _isSending = true;
            shouldSend = true;
        }

        if (shouldSend)
        {
            try
            {
                await SendDataInternalAsync(data);
                await ProcessSendQueueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send error for client {ClientId}", ClientId);
                await DisconnectAsync("Send error");
            }
            finally
            {
                lock (_sendLock)
                {
                    _isSending = false;
                }
            }
        }
    }

    private async Task SendDataInternalAsync(byte[] data)
    {
        var tcs = new TaskCompletionSource<bool>();
        var sendArgs = new SocketAsyncEventArgs();

        try
        {
            sendArgs.SetBuffer(data, 0, data.Length);
            sendArgs.Completed += (sender, e) =>
            {
                try
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        _logger.LogTrace("Successfully sent {ByteCount} bytes to client {ClientId}",
                            e.BytesTransferred, ClientId);
                        tcs.TrySetResult(true);
                    }
                    else
                    {
                        _logger.LogWarning("Send failed for client {ClientId}: {Error}",
                            ClientId, e.SocketError);
                        tcs.TrySetException(new SocketException((int)e.SocketError));
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    e.Dispose();
                }
            };

            _logger.LogTrace("Sending {ByteCount} bytes to client {ClientId}", data.Length, ClientId);

            if (!_socket.SendAsync(sendArgs))
            {
                if (sendArgs.SocketError == SocketError.Success)
                {
                    _logger.LogTrace("Successfully sent {ByteCount} bytes to client {ClientId} (sync)",
                        sendArgs.BytesTransferred, ClientId);
                    tcs.TrySetResult(true);
                }
                else
                {
                    _logger.LogWarning("Send failed for client {ClientId} (sync): {Error}",
                        ClientId, sendArgs.SocketError);
                    tcs.TrySetException(new SocketException((int)sendArgs.SocketError));
                }
                sendArgs.Dispose();
            }

            await tcs.Task;
        }
        catch (ObjectDisposedException)
        {
            _logger.LogDebug("Socket disposed during send for client {ClientId}", ClientId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during send setup for client {ClientId}", ClientId);
            sendArgs?.Dispose();
            throw;
        }
    }

    private async Task ProcessSendQueueAsync()
    {
        while (true)
        {
            byte[]? nextData = null;

            lock (_sendLock)
            {
                if (_sendQueue.Count == 0)
                    break;

                nextData = _sendQueue.Dequeue();
            }

            if (nextData != null)
            {
                _logger.LogTrace("Processing queued send ({ByteCount} bytes) for client {ClientId}",
                    nextData.Length, ClientId);
                await SendDataInternalAsync(nextData);
            }
        }
    }

    private async Task ProcessPacketsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                if (!_dhKeySet && !_firstPacketReceived)
                {
                    // Process first dummy packet - receive length + payload + 8 signature bytes
                    await ProcessFirstDummyPacketAsync(cancellationToken);
                    _firstPacketReceived = true;
                    continue;
                }

                if (!_dhKeySet)
                {
                    // Process DH key packet - static 1024 bytes
                    await ProcessDHKeyAsync(cancellationToken);
                }
                else
                {
                    // Process game packets - length + payload + 8 signature bytes
                    await ProcessGamePacketsAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing packets for client {ClientId}", ClientId);
                break;
            }
        }
    }

    private async Task ProcessFirstDummyPacketAsync(CancellationToken cancellationToken)
    {
        // First, receive the packet length (2 bytes)
        var lengthBuffer = new byte[2];
        var received = await ReceiveExactBytesAsync(lengthBuffer, 0, 2, cancellationToken);
        if (received != 2)
        {
            _logger.LogDebug("Client {ClientId} disconnected during first packet length", ClientId);
            return;
        }

        // Get the payload length from the first 2 bytes
        var payloadLength = BitConverter.ToUInt16(lengthBuffer, 0);
        var totalPacketSize = payloadLength;
        _logger.LogDebug("First dummy packet from client {ClientId}: payload={PayloadLength}, total={TotalSize}",
            ClientId, payloadLength, totalPacketSize);

        // Receive the rest of the packet (payload + signature)
        var packetBuffer = new byte[totalPacketSize + 2]; // +2 for the length bytes we already read
        Array.Copy(lengthBuffer, 0, packetBuffer, 0, 2);

        received = await ReceiveExactBytesAsync(packetBuffer, 2, totalPacketSize - 2, cancellationToken);
        if (received != totalPacketSize)
        {
            _logger.LogDebug("Client {ClientId} disconnected during first packet data", ClientId);
            return;
        }

        _logger.LogDebug("Received first dummy packet from client {ClientId} ({ByteCount} bytes)",
            ClientId, packetBuffer.Length);
    }

    private async Task ProcessDHKeyAsync(CancellationToken cancellationToken)
    {
        // DH key packet is always exactly 1024 bytes
        const int DH_KEY_PACKET_SIZE = 1024;
        var recvBuffer = new byte[DH_KEY_PACKET_SIZE];
        var received = await ReceiveExactBytesAsync(recvBuffer, 0, DH_KEY_PACKET_SIZE, cancellationToken);
        var dhBuffer = new byte[received];
        Array.Copy(recvBuffer, dhBuffer, received);

        _logger.LogTrace("Received DH key packet ({ByteCount} bytes) from client {ClientId}",
            received, ClientId);

        // Decrypt the DH key data
        var decryptedBuffer = new byte[received];

        if (_cryptographer.IsInitialized)
        {
            _cryptographer.Decrypt(dhBuffer, decryptedBuffer);
            _logger.LogTrace("Decrypted DH key buffer for client {ClientId}", ClientId);
        }
        else
        {
            Array.Copy(dhBuffer, decryptedBuffer, received);
            _logger.LogTrace("Using unencrypted DH key buffer for client {ClientId}", ClientId);
        }

        // Check if we have complete DH key packet
        var bufferText = Encoding.ASCII.GetString(decryptedBuffer);
        if (bufferText.Contains("TQClient"))
        {
            try
            {
                _logger.LogDebug("Processing complete DH key packet from client {ClientId}", ClientId);

                using var packet = new ConquerPacket(decryptedBuffer);

                if (packet.TryExtractDHKey(out var clientPublicKey))
                {
                    _logger.LogDebug("Extracted DH public key from client {ClientId}: {PublicKey}",
                        ClientId, clientPublicKey[..16] + "...");

                    _dhKeyExchange.HandleResponse(clientPublicKey);

                    var sharedSecret = _dhKeyExchange.GetSharedSecret();
                    var finalKey = PostProcessDHKey(sharedSecret);

                    _cryptographer.GenerateKey(finalKey);
                    _cryptographer.Reset();
                    _dhKeySet = true;

                    _logger.LogInformation("DH key exchange completed successfully for client {ClientId}", ClientId);

                    // Check for extra packet data after DH key
                    await CheckForExtraPacketDataAsync(decryptedBuffer);
                }
                else
                {
                    _logger.LogWarning("Failed to extract DH key from client {ClientId}", ClientId);
                    await DisconnectAsync("Invalid DH key");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing DH key for client {ClientId}", ClientId);
                await DisconnectAsync("DH key processing error");
            }
        }
        else
        {
            _logger.LogDebug("DH key packet incomplete, waiting for more data from client {ClientId}", ClientId);
        }
    }

    private async Task CheckForExtraPacketDataAsync(byte[] dhKeyBuffer)
    {
        // Look for extra packet data at the end of the DH key buffer
        var extraDataStart = Array.LastIndexOf(dhKeyBuffer, (byte)'T');
        if (extraDataStart > 0 && extraDataStart < dhKeyBuffer.Length - 60)
        {
            var possibleExtraStart = extraDataStart + 8; // Skip "TQClient"
            if (possibleExtraStart + 60 <= dhKeyBuffer.Length)
            {
                _logger.LogDebug("Processing extra packet data for client {ClientId}", ClientId);

                var extraData = dhKeyBuffer.AsSpan(possibleExtraStart, 60);
                var decryptedExtra = new byte[60];

                _cryptographer.Decrypt(extraData, decryptedExtra);

                // Process as game packet
                using var packet = new ConquerPacket(decryptedExtra);
                if (packet.IsComplete)
                {
                    var message = new ClientMessage(ClientId, packet);
                    await _messageWriter.WriteAsync(message);
                }
            }
        }
    }

    // This is the key method that needs to match SecuritySocket behavior
    private async Task ProcessGamePacketsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsConnected && _dhKeySet)
        {
            try
            {
                // First, receive the packet length (2 bytes)
                var lengthBuffer = new byte[2];
                var received = await ReceiveExactBytesAsync(lengthBuffer, 0, 2, cancellationToken);
                if (received != 2)
                {
                    _logger.LogDebug("Client {ClientId} disconnected while receiving game packet header", ClientId);
                    return;
                }

                // Decrypt the length header
                if (_cryptographer.IsInitialized)
                {
                    var decryptedLength = new byte[2];
                    _cryptographer.Decrypt(lengthBuffer, decryptedLength);
                    Array.Copy(decryptedLength, lengthBuffer, 2);
                }

                // Get the payload length and calculate total packet size
                var payloadLength = BitConverter.ToUInt16(lengthBuffer, 0);
                var totalPacketSize = payloadLength + 8; // Add 8 bytes for signature

                if (totalPacketSize > 1024 || totalPacketSize < 8)
                {
                    _logger.LogWarning("Invalid game packet length {Length} from client {ClientId}", totalPacketSize + 2, ClientId);
                    await DisconnectAsync("Invalid packet length");
                    return;
                }

                _logger.LogTrace("Game packet from client {ClientId}: payload={PayloadLength}, total={TotalSize}",
                    ClientId, payloadLength, totalPacketSize);

                // Create buffer for the complete packet (length + payload + signature)
                var packetBuffer = new byte[totalPacketSize + 2]; // +2 for length header
                Array.Copy(lengthBuffer, 0, packetBuffer, 0, 2);

                // Receive the rest of the packet (payload + signature)
                received = await ReceiveExactBytesAsync(packetBuffer, 2, totalPacketSize - 2, cancellationToken);
                if (received != totalPacketSize)
                {
                    _logger.LogDebug("Client {ClientId} disconnected while receiving game packet data", ClientId);
                    return;
                }

                // Decrypt the payload + signature part
                if (_cryptographer.IsInitialized)
                {
                    var encryptedPart = packetBuffer.AsSpan(2, totalPacketSize);
                    var decryptedPart = new byte[totalPacketSize];
                    _cryptographer.Decrypt(encryptedPart, decryptedPart);
                    decryptedPart.CopyTo(packetBuffer.AsSpan(2));
                }

                _logger.LogTrace("Received complete game packet ({ByteCount} bytes) from client {ClientId}",
                    packetBuffer.Length, ClientId);

                try
                {
                    using var packet = new ConquerPacket(packetBuffer);

                    if (packet.IsComplete)
                    {
                        _logger.LogDebug("Received game packet type {PacketType} from client {ClientId}",
                            packet.Type, ClientId);

                        var message = new ClientMessage(ClientId, packet);
                        await _messageWriter.WriteAsync(message);
                    }
                    else
                    {
                        _logger.LogWarning("Incomplete game packet from client {ClientId}", ClientId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing game packet for client {ClientId}", ClientId);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in game packet processing for client {ClientId}", ClientId);
                break;
            }
        }
    }

    // Helper method to receive exact number of bytes
    private async Task<int> ReceiveExactBytesAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int totalReceived = 0;

        if (IsConnected && !cancellationToken.IsCancellationRequested)
        {
            var received = await ReceiveDataDirectAsync(buffer, offset, count, cancellationToken);
            totalReceived += received;
        }

        return totalReceived;
    }
    // Direct receive with offset and count - matching SecuritySocket behavior
    private async Task<int> ReceiveDataDirectAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<int>();
        var receiveArgs = new SocketAsyncEventArgs();

        try
        {
            receiveArgs.SetBuffer(buffer, offset, count);
            receiveArgs.Completed += (sender, e) =>
            {
                try
                {
                    if (e.SocketError == SocketError.Success)
                    {
                        tcs.TrySetResult(e.BytesTransferred);
                    }
                    else
                    {
                        tcs.TrySetException(new SocketException((int)e.SocketError));
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    e.Dispose();
                }
            };

            using var registration = cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled(cancellationToken);
            });

            if (!_socket.ReceiveAsync(receiveArgs))
            {
                if (receiveArgs.SocketError == SocketError.Success)
                {
                    tcs.TrySetResult(receiveArgs.BytesTransferred);
                }
                else
                {
                    tcs.TrySetException(new SocketException((int)receiveArgs.SocketError));
                }
                receiveArgs.Dispose();
            }

            return await tcs.Task;
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during direct receive for client {ClientId}", ClientId);
            receiveArgs?.Dispose();
            throw;
        }
    }

    private static byte[] PostProcessDHKey(byte[] sharedSecret)
    {
        using var md5 = MD5.Create();

        var effectiveLength = Array.IndexOf(sharedSecret, (byte)0);
        if (effectiveLength == -1) effectiveLength = sharedSecret.Length;

        var hash1 = md5.ComputeHash(sharedSecret, 0, effectiveLength);
        var hex1 = Convert.ToHexString(hash1).ToLowerInvariant();

        var combined = hex1 + hex1;
        var hash2 = md5.ComputeHash(Encoding.ASCII.GetBytes(combined));
        var hex2 = Convert.ToHexString(hash2).ToLowerInvariant();

        var finalHex = hex1 + hex2;
        return Encoding.ASCII.GetBytes(finalHex);
    }

    public async ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData)
    {
        if (_disposed || !IsConnected)
        {
            _logger.LogTrace("Skipped send to disconnected client {ClientId}", ClientId);
            return;
        }

        try
        {
            var dataToSend = packetData.ToArray();

            if (_dhKeySet && _cryptographer.IsInitialized)
            {
                var encrypted = new byte[dataToSend.Length];
                _cryptographer.Encrypt(dataToSend, encrypted);

                _logger.LogTrace("Sending encrypted packet ({ByteCount} bytes) to client {ClientId}",
                    encrypted.Length, ClientId);

                await SendDataAsync(encrypted);
            }
            else
            {
                _logger.LogTrace("Sending unencrypted packet ({ByteCount} bytes) to client {ClientId}",
                    dataToSend.Length, ClientId);

                await SendDataAsync(dataToSend);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send packet to client {ClientId}", ClientId);
            await DisconnectAsync("Send failed");
        }
    }

    public async ValueTask DisconnectAsync(string reason = "")
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disconnecting client {ClientId}: {Reason}", ClientId, reason);
        Dispose();
        await ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _logger.LogDebug("Disposing client {ClientId}", ClientId);

            lock (_sendLock)
            {
                _sendQueue.Clear();
            }

            _tcpClient.Close();
            _cryptographer.Dispose();
            Array.Clear(_dhKeyBuffer);
            Array.Clear(_sendBuffer);
            Array.Clear(_recvBuffer);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing client {ClientId}", ClientId);
        }
    }
}