using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MMORPGServer.Domain.Common.Constants;
using MMORPGServer.Domain.Common.Interfaces;
using MMORPGServer.Domain.ValueObjects;
using MMORPGServer.Infrastructure.Networking.Clients;
using MMORPGServer.Infrastructure.Networking.Security;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MMORPGServer.Infrastructure.Networking.Server
{
    public sealed class GameServer : IGameServer, IDisposable
    {
        private readonly ILogger<GameServer> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly INetworkManager _networkManager;
        private readonly IPacketHandler _packetHandler;

        private readonly ChannelWriter<ClientMessage> _messageWriter;
        private readonly ChannelReader<ClientMessage> _messageReader;

        private TcpListener _tcpListener;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _acceptTask;
        private Task _messageProcessingTask;
        private int _nextClientId = 1;
        private long _totalConnectionsAccepted = 0;
        private long _totalMessagesProcessed = 0;
        private DateTime _serverStartTime;

        public GameServer(
            ILogger<GameServer> logger,
            IServiceProvider serviceProvider,
            INetworkManager networkManager,
            IPacketHandler packetHandler)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _networkManager = networkManager;
            _packetHandler = packetHandler;
            Channel<ClientMessage> channel = Channel.CreateUnbounded<ClientMessage>();
            _messageWriter = channel.Writer;
            _messageReader = channel.Reader;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _serverStartTime = DateTime.UtcNow;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _logger.LogInformation("Binding to port {Port}...", ServerConstants.DEFAULT_PORT);

            _tcpListener = new TcpListener(IPAddress.Any, ServerConstants.DEFAULT_PORT);
            _tcpListener.Start();

            _logger.LogInformation("TCP Listener started successfully");
            _logger.LogInformation("Server accepting connections on 0.0.0.0:{Port}", ServerConstants.DEFAULT_PORT);
            _logger.LogInformation("Maximum clients: {MaxClients}", ServerConstants.MAX_CLIENTS);

            _acceptTask = AcceptClientsAsync(_cancellationTokenSource.Token);
            _messageProcessingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);

            _logger.LogInformation("Game server is now online and ready!");
            await Task.CompletedTask;
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Client acceptance loop started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient tcpClient = await _tcpListener!.AcceptTcpClientAsync();
                    int clientId = Interlocked.Increment(ref _nextClientId);
                    Interlocked.Increment(ref _totalConnectionsAccepted);

                    string clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";

                    // Check if we're at capacity
                    if (_networkManager.ConnectionCount >= ServerConstants.MAX_CLIENTS)
                    {
                        _logger.LogWarning("Connection rejected from {ClientEndpoint} - Server at capacity ({Current}/{Max})",
                            clientEndpoint, _networkManager.ConnectionCount, ServerConstants.MAX_CLIENTS);
                        tcpClient.Close();
                        continue;
                    }

                    DiffieHellmanKeyExchange dhKeyExchange = _serviceProvider.GetRequiredService<DiffieHellmanKeyExchange>();
                    TQCast5Cryptographer cryptographer = _serviceProvider.GetRequiredService<TQCast5Cryptographer>();

                    GameClient gameClient = new GameClient(
                        clientId,
                        tcpClient,
                        dhKeyExchange,
                        cryptographer,
                        _messageWriter,
                        _serviceProvider.GetRequiredService<ILogger<GameClient>>()
                    );

                    _networkManager.AddClient(gameClient);
                    _ = Task.Run(async () =>
                    {
                        await gameClient.StartAsync(cancellationToken);
                    }, cancellationToken);

                    _logger.LogInformation("Player #{ClientId} connected from {ClientEndpoint} (Total: {CurrentConnections}/{MaxConnections})",
                        clientId, clientEndpoint, _networkManager.ConnectionCount, ServerConstants.MAX_CLIENTS);

                    // Log milestone connections
                    if (_totalConnectionsAccepted % 100 == 0)
                    {
                        _logger.LogInformation("Milestone reached: {TotalConnections} total connections accepted since server start",
                            _totalConnectionsAccepted);
                    }
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("TCP Listener disposed, stopping client acceptance");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting client connection");
                    await Task.Delay(1000, cancellationToken); // Brief delay to prevent rapid error loops
                }
            }

            _logger.LogDebug("Client acceptance loop stopped");
        }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Message processing loop started");

            await foreach (ClientMessage message in _messageReader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    message.Packet.Seek(4);
                    await _packetHandler.HandlePacketAsync(message.Client, message.Packet);
                    Interlocked.Increment(ref _totalMessagesProcessed);

                    // Log message processing milestones
                    if (_totalMessagesProcessed % 10000 == 0)
                    {
                        _logger.LogDebug("Processed {TotalMessages} total messages", _totalMessagesProcessed);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from client {ClientId} (Type: {PacketType})",
                       message.Client.ClientId, message.Packet.Type);
                }
            }

            _logger.LogDebug("Message processing loop stopped");
        }

        public void RemoveClient(int clientId)
        {
            _networkManager.RemoveClient(clientId);
            _logger.LogInformation("Player #{ClientId} disconnected (Remaining: {CurrentConnections})",
                clientId, _networkManager.ConnectionCount);
        }

        public async ValueTask BroadcastPacketAsync(ReadOnlyMemory<byte> packetData, int excludeClientId = 0)
        {
            int connectedClients = _networkManager.ConnectionCount;
            if (connectedClients > 0)
            {
                _logger.LogDebug("Broadcasting packet to {ClientCount} clients", connectedClients);
                await _networkManager.BroadcastAsync(packetData, excludeClientId);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            TimeSpan uptime = DateTime.UtcNow - _serverStartTime;

            _logger.LogWarning("Initiating server shutdown...");
            _logger.LogInformation("Final Statistics:");
            _logger.LogInformation("   Total Uptime: {Uptime}", uptime.ToString(@"dd\.hh\:mm\:ss"));
            _logger.LogInformation("   Total Connections: {TotalConnections}", _totalConnectionsAccepted);
            _logger.LogInformation("   Messages Processed: {TotalMessages}", _totalMessagesProcessed);
            _logger.LogInformation("   Active Connections: {ActiveConnections}", _networkManager.ConnectionCount);

            _cancellationTokenSource?.Cancel();
            _messageWriter.Complete();

            _logger.LogInformation("Stopping TCP listener...");
            _tcpListener?.Stop();

            List<Task> tasks = new List<Task>();
            if (_acceptTask is not null) tasks.Add(_acceptTask);
            if (_messageProcessingTask is not null) tasks.Add(_messageProcessingTask);

            _logger.LogInformation("Waiting for background tasks to complete...");
            await Task.WhenAll(tasks);

            _logger.LogInformation("Server shutdown sequence completed");
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _tcpListener?.Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}