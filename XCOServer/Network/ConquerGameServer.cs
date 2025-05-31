// Network/ConquerGameServer.cs - Updated server using ConquerSecurityClient
namespace MMORPGServer.Network
{
    public sealed class ConquerGameServer : IGameServer, IDisposable
    {
        private readonly ILogger<ConquerGameServer> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly INetworkManager _networkManager;
        private readonly IPacketHandler _packetHandler;

        private readonly ChannelWriter<ClientMessage> _messageWriter;
        private readonly ChannelReader<ClientMessage> _messageReader;

        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _acceptTask;
        private Task? _messageProcessingTask;
        private uint _nextClientId = 1;

        public ConquerGameServer(
            ILogger<ConquerGameServer> logger,
            IServiceProvider serviceProvider,
            INetworkManager networkManager,
            IPacketHandler packetHandler)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _networkManager = networkManager;
            _packetHandler = packetHandler;

            var channel = Channel.CreateUnbounded<ClientMessage>();

            _messageWriter = channel.Writer;
            _messageReader = channel.Reader;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _tcpListener = new TcpListener(IPAddress.Any, DEFAULT_PORT);
            _tcpListener.Start();

            _logger.LogInformation($"Conquer MMORPG Server started on port {DEFAULT_PORT}");

            _acceptTask = AcceptClientsAsync(_cancellationTokenSource.Token);
            _messageProcessingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);

            await Task.CompletedTask;
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _tcpListener!.AcceptTcpClientAsync();
                    var clientId = Interlocked.Increment(ref _nextClientId);

                    var dhKeyExchange = _serviceProvider.GetRequiredService<IDHKeyExchange>();
                    var cryptographer = _serviceProvider.GetRequiredService<ICryptographer>();

                    var gameClient = new ConquerSecurityClient(
                        clientId,
                        tcpClient,
                        dhKeyExchange,
                        cryptographer,
                        _messageWriter,
                        _serviceProvider.GetRequiredService<ILogger<ConquerSecurityClient>>()
                    );

                    _networkManager.AddClient(gameClient);
                    _ = Task.Run(() => gameClient.StartAsync(cancellationToken), cancellationToken);

                    _logger.LogInformation("Conquer client {ClientId} connected from {EndPoint} (Total: {Count})",
                        clientId, tcpClient.Client.RemoteEndPoint, _networkManager.ConnectionCount);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client connection");
                }
            }
        }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            await foreach (var message in _messageReader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var client = _networkManager.GetClient(message.ClientId);
                    if (client is not null)
                    {
                        await _packetHandler.HandlePacketAsync(client, message.Packet);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from client {ClientId}", message.ClientId);
                }
            }
        }

        public void RemoveClient(uint clientId)
        {
            _networkManager.RemoveClient(clientId);
        }

        public async ValueTask BroadcastPacketAsync(ReadOnlyMemory<byte> packetData, uint excludeClientId = 0)
        {
            await _networkManager.BroadcastAsync(packetData, excludeClientId);
        }

        public async ValueTask BroadcastToMapAsync(uint mapId, ReadOnlyMemory<byte> packetData, uint excludeClientId = 0)
        {
            await _networkManager.BroadcastToMapAsync(mapId, packetData, excludeClientId);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Shutting down Conquer MMORPG Server...");

            _cancellationTokenSource?.Cancel();
            _messageWriter.Complete();

            _tcpListener?.Stop();

            var tasks = new List<Task>();
            if (_acceptTask is not null) tasks.Add(_acceptTask);
            if (_messageProcessingTask is not null) tasks.Add(_messageProcessingTask);

            await Task.WhenAll(tasks);

            _logger.LogInformation("Server shutdown complete");
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _tcpListener?.Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}