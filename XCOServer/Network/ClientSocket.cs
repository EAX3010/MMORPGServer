using MMORPGServer.Core;
using MMORPGServer.Security;

public sealed class ClientSocket : IGameClient
{
    // A constant for the maximum allowed packet size.
    private const int MAX_PACKET_SIZE = 1024;

    public uint ClientId { get; }
    public IPlayer? Player { get; set; }
    public bool IsConnected => State != ClientState.Disconnected && _tcpClient.Connected;
    public string? IPAddress { get; }
    public DateTime ConnectedAt { get; }
    public ClientState State { get; private set; }

    private readonly TcpClient _tcpClient;
    private readonly Socket _socket;
    private readonly ILogger<ClientSocket> _logger;
    private readonly DiffieHellmanKeyExchange _dhKeyExchange;
    private readonly TQCast5Cryptographer _cryptographer;
    private readonly ChannelWriter<ClientMessage> _messageWriter;

    private readonly Channel<ReadOnlyMemory<byte>> _sendChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Memory<byte> _receiveBuffer = new byte[8192];
    private int _receiveBufferOffset = 0;

    public ClientSocket(
        uint clientId,
        TcpClient tcpClient,
        DiffieHellmanKeyExchange dhKeyExchange,
        TQCast5Cryptographer cryptographer,
        ChannelWriter<ClientMessage> messageWriter,
        ILogger<ClientSocket> logger)
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
        State = ClientState.Connecting;

        _socket.NoDelay = true;
        _socket.SendBufferSize = 8192;
        _socket.ReceiveBufferSize = 8192;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);
        var processTasks = new[]
        {
            ProcessIncomingDataAsync(linkedCts.Token),
            ProcessOutgoingPacketsAsync(linkedCts.Token)
        };

        try
        {
            await SendDHKeyExchangeAsync();
            State = ClientState.WaitingForDummyPacket;
            await Task.WhenAny(processTasks);
        }
        catch (OperationCanceledException) { /* Expected on disconnect */ }
        catch (Exception ex) { _logger.LogError(ex, "Critical error in client {ClientId}", ClientId); }
        finally { await DisconnectAsync("Connection terminated"); }
    }

    public async ValueTask SendPacketAsync(ReadOnlyMemory<byte> packetData)
    {
        if (State == ClientState.Disconnected) return;
        if (packetData.Length > MAX_PACKET_SIZE)
        {
            _logger.LogError("Attempted to send packet larger than MAX_PACKET_SIZE ({PacketLength} > {MaxPacketSize})", packetData.Length, MAX_PACKET_SIZE);
            return;
        }
        await _sendChannel.Writer.WriteAsync(packetData, _cancellationTokenSource.Token);
    }

    private async Task ProcessOutgoingPacketsAsync(CancellationToken cancellationToken)
    {
        await foreach (var packetMemory in _sendChannel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                var dataToSend = packetMemory;
                if (State == ClientState.Connected && _cryptographer.IsInitialized)
                {
                    var encryptedMemory = new byte[dataToSend.Length];
                    _cryptographer.Encrypt(dataToSend.Span, encryptedMemory);
                    dataToSend = encryptedMemory;
                }
                await _socket.SendAsync(dataToSend, SocketFlags.None, cancellationToken);
            }
            catch (Exception ex)
            {
                await DisconnectAsync($"Send error: {ex.GetType().Name}");
                break;
            }
        }
    }

    private async Task ProcessIncomingDataAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bufferFreeSpace = _receiveBuffer.Slice(_receiveBufferOffset);
                var bytesRead = await _socket.ReceiveAsync(bufferFreeSpace, SocketFlags.None, cancellationToken);
                if (bytesRead == 0)
                    break;

                _receiveBufferOffset += bytesRead;
                ProcessBuffer();
            }
            catch (Exception ex)
            {
                await DisconnectAsync($"Receive error: {ex.GetType().Name}");
                break;
            }
        }
    }

    private void ProcessBuffer()
    {
        while (TryProcessPacket(out var consumedLength))
        {
            if (consumedLength == 0)
                break; // No full packet was processed

            var remainingLength = _receiveBufferOffset - consumedLength;
            if (remainingLength >= 0)
            {
                _receiveBuffer.Slice(consumedLength, remainingLength).CopyTo(_receiveBuffer);
            }
            _receiveBufferOffset = remainingLength;
        }
    }

    private bool TryProcessPacket(out int consumedLength)
    {
        consumedLength = 0;
        var bufferSpan = _receiveBuffer.Span.Slice(0, _receiveBufferOffset);

        switch (State)
        {
            case ClientState.WaitingForDummyPacket:
                return TryProcessSimplePacket(bufferSpan, out consumedLength, () =>
                {
                    State = ClientState.DhKeyExchange;
                });

            case ClientState.DhKeyExchange:
                HandleDhKeyPacket(bufferSpan);
                consumedLength = bufferSpan.Length;
                return true;

            case ClientState.Connected:
                return TryProcessEncryptedPacket(bufferSpan, out consumedLength);
        }

        return false;
    }

    private bool TryProcessSimplePacket(ReadOnlySpan<byte> buffer, out int consumedLength, Action onPacketProcessed)
    {
        consumedLength = 0;
        if (buffer.Length < 2) return false;

        var packetLength = BitConverter.ToUInt16(buffer);

        // ** NEW: Validate packet size **
        if (packetLength > MAX_PACKET_SIZE)
        {
            _logger.LogWarning("Client {ClientId} sent an oversized packet (Dummy: {PacketLength} > {MaxPacketSize}). Disconnecting.", ClientId, packetLength, MAX_PACKET_SIZE);
            DisconnectAsync("Oversized packet").GetAwaiter().GetResult();
            return false; // Stop processing
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
        if (buffer.Length < 2) return false;

        Span<byte> decryptedLengthBytes = stackalloc byte[2];
        _cryptographer.Decrypt(buffer.Slice(0, 2), decryptedLengthBytes);
        var packetLength = BitConverter.ToUInt16(decryptedLengthBytes);

        // ** NEW: Validate packet size **
        if (packetLength > MAX_PACKET_SIZE)
        {
            _logger.LogWarning("Client {ClientId} sent an oversized packet (Game: {PacketLength} > {MaxPacketSize}). Disconnecting.", ClientId, packetLength, MAX_PACKET_SIZE);
            DisconnectAsync("Oversized packet").GetAwaiter().GetResult();
            return false; // Stop processing
        }
        packetLength += 8;
        if (buffer.Length < packetLength) return false;

        consumedLength = packetLength;
        var packetData = buffer.Slice(0, packetLength);

        Span<byte> decryptedPacketBytes = packetLength <= 512 ? stackalloc byte[packetLength] : new byte[packetLength];
        _cryptographer.Decrypt(packetData, decryptedPacketBytes);

        using var packet = new Packet(decryptedPacketBytes.ToArray());
        _messageWriter.TryWrite(new ClientMessage(ClientId, packet));

        return true;
    }
    private async Task SendDHKeyExchangeAsync()
    {
        var defaultKey = Encoding.ASCII.GetBytes("R3Xx97ra5j8D6uZz");
        _cryptographer.GenerateKey(defaultKey);
        var memory = _dhKeyExchange.CreateDHKeyPacket();
        var encryptedPacket = new byte[memory.Length];
        _cryptographer.Encrypt(memory.Span, encryptedPacket);
        await _socket.SendAsync(encryptedPacket, SocketFlags.None);
    }


    private void HandleDhKeyPacket(ReadOnlySpan<byte> dhKeyBuffer)
    {
        Span<byte> decryptedPacketBytes = new byte[dhKeyBuffer.Length];
        _cryptographer.Decrypt(dhKeyBuffer, decryptedPacketBytes);
        using var packet = new Packet(decryptedPacketBytes.ToArray());
        var x = Encoding.ASCII.GetString(decryptedPacketBytes);
        if (packet.TryExtractDHKey(out var clientPublicKey))
        {
            _dhKeyExchange.HandleResponse(clientPublicKey);
            var finalKey = _dhKeyExchange.GetSecret();
            _cryptographer.GenerateKey(finalKey);
            _cryptographer.Reset();
            State = ClientState.Connected;
            _logger.LogInformation("DH key exchange completed successfully for client {ClientId}", ClientId);
        }
        else
        {
            DisconnectAsync("Invalid DH key").GetAwaiter().GetResult();
        }
    }



    public async ValueTask DisconnectAsync(string reason = "")
    {
        if (State == ClientState.Disconnected)
            return;
        State = ClientState.Disconnected;
        _logger.LogInformation("Disconnecting client {ClientId}: {Reason}", ClientId, reason);
        _cancellationTokenSource.Cancel();
        _sendChannel.Writer.TryComplete();
        _tcpClient.Close();
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        if (State != ClientState.Disconnected)
        {
            DisconnectAsync("Disposed").GetAwaiter().GetResult();
        }
        _cancellationTokenSource.Dispose();
    }


}