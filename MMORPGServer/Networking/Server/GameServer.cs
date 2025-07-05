using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets;
using MMORPGServer.Networking.Security;
using MMORPGServer.Services;
using Serilog;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MMORPGServer.Networking.Server
{
    public sealed class GameServer : IDisposable
    {
        private readonly NetworkManager _networkManager;
        private readonly PacketHandler _packetHandler;

        private readonly ChannelWriter<ClientMessage> _messageWriter;
        private readonly ChannelReader<ClientMessage> _messageReader;

        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _acceptTask;
        private Task? _messageProcessingTask;
        private int _nextClientId = 1;
        private long _totalConnectionsAccepted = 0;
        private long _totalMessagesProcessed = 0;
        private DateTime _serverStartTime;

        public GameServer(NetworkManager networkManager, PacketHandler packetHandler)
        {
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _packetHandler = packetHandler ?? throw new ArgumentNullException(nameof(packetHandler));

            var channel = Channel.CreateUnbounded<ClientMessage>();
            _messageWriter = channel.Writer;
            _messageReader = channel.Reader;

            Log.Debug("GameServer initialized");
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _serverStartTime = DateTime.UtcNow;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Get port from configuration
            int port = GameServerConfig.ServerPort;

            Log.Information("Binding to port {Port}...", port);

            _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListener.Start();

            Log.Information("TCP Listener started successfully");
            Log.Information("Server accepting connections on 0.0.0.0:{Port}", port);
            Log.Information("Maximum clients: {MaxClients}", GameServerConfig.MaxPlayers);

            _acceptTask = AcceptClientsAsync(_cancellationTokenSource.Token);
            _messageProcessingTask = ProcessMessagesAsync(_cancellationTokenSource.Token);

            Log.Information("Game server is now online and ready!");
            await Task.CompletedTask;
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            Log.Debug("Client acceptance loop started");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_tcpListener == null) break;

                    TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    int clientId = Interlocked.Increment(ref _nextClientId);
                    Interlocked.Increment(ref _totalConnectionsAccepted);

                    string clientEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";

                    // Check if we're at capacity
                    int maxClients = GameServerConfig.MaxPlayers;
                    if (_networkManager.ConnectionCount >= maxClients)
                    {
                        Log.Warning("Connection rejected from {ClientEndpoint} - Server at capacity ({Current}/{Max})",
                            clientEndpoint, _networkManager.ConnectionCount, maxClients);
                        tcpClient.Close();
                        continue;
                    }

                    // Create cryptographic services directly
                    var dhKeyExchange = new DiffieHellmanKeyExchange();
                    var cryptographer = new TQCast5Cryptographer();

                    // Create GameClient directly
                    var gameClient = new GameClient(
                        clientId,
                        tcpClient,
                        dhKeyExchange,
                        cryptographer,
                        _messageWriter
                    );

                    _networkManager.AddClient(gameClient);

                    // Start client processing in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await gameClient.StartAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error in client {ClientId} processing", clientId);
                        }
                        finally
                        {
                            // Ensure client is removed when done
                            RemoveClient(clientId);
                        }
                    }, cancellationToken);

                    Log.Information("Player #{ClientId} connected from {ClientEndpoint} (Total: {CurrentConnections}/{MaxConnections})",
                        clientId, clientEndpoint, _networkManager.ConnectionCount, maxClients);

                    // Log milestone connections
                    if (_totalConnectionsAccepted % 100 == 0)
                    {
                        Log.Information("Milestone reached: {TotalConnections} total connections accepted since server start",
                            _totalConnectionsAccepted);
                    }
                }
                catch (ObjectDisposedException)
                {
                    Log.Debug("TCP Listener disposed, stopping client acceptance");
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    Log.Debug("TCP Listener interrupted, stopping client acceptance");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error accepting client connection");
                    await Task.Delay(1000, cancellationToken); // Brief delay to prevent rapid error loops
                }
            }

            Log.Debug("Client acceptance loop stopped");
        }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            Log.Debug("Message processing loop started");

            try
            {
                await foreach (ClientMessage message in _messageReader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        // Reset packet position for processing
                        message.Packet.Seek(4);

                        // Process the packet
                        await _packetHandler.HandlePacketAsync(message.Client, message.Packet);

                        Interlocked.Increment(ref _totalMessagesProcessed);

                        // Log message processing milestones
                        if (_totalMessagesProcessed % 10000 == 0)
                        {
                            Log.Debug("Processed {TotalMessages} total messages", _totalMessagesProcessed);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing message from client {ClientId} (Type: {PacketType})",
                           message.Client.ClientId, message.Packet.Type);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Message processing cancelled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in message processing loop");
            }

            Log.Debug("Message processing loop stopped");
        }

        public void RemoveClient(int clientId)
        {
            _networkManager.RemoveClient(clientId);
            Log.Information("Player #{ClientId} disconnected (Remaining: {CurrentConnections})",
                clientId, _networkManager.ConnectionCount);
        }

        public async ValueTask BroadcastPacketAsync(ReadOnlyMemory<byte> packetData, int excludeClientId = 0)
        {
            int connectedClients = _networkManager.ConnectionCount;
            if (connectedClients > 0)
            {
                Log.Debug("Broadcasting packet to {ClientCount} clients", connectedClients);
                await _networkManager.BroadcastAsync(packetData, excludeClientId);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            TimeSpan uptime = DateTime.UtcNow - _serverStartTime;

            Log.Warning("Initiating server shutdown...");
            Log.Information("Final Statistics:");
            Log.Information("   Total Uptime: {Uptime}", uptime.ToString(@"dd\.hh\:mm\:ss"));
            Log.Information("   Total Connections: {TotalConnections}", _totalConnectionsAccepted);
            Log.Information("   Messages Processed: {TotalMessages}", _totalMessagesProcessed);
            Log.Information("   Active Connections: {ActiveConnections}", _networkManager.ConnectionCount);

            // Cancel all operations
            _cancellationTokenSource?.Cancel();
            _messageWriter.Complete();

            Log.Information("Stopping TCP listener...");
            _tcpListener?.Stop();

            // Wait for background tasks
            var tasks = new List<Task>();
            if (_acceptTask != null) tasks.Add(_acceptTask);
            if (_messageProcessingTask != null) tasks.Add(_messageProcessingTask);

            if (tasks.Count > 0)
            {
                Log.Information("Waiting for background tasks to complete...");
                try
                {
                    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (TimeoutException)
                {
                    Log.Warning("Some background tasks did not complete within timeout");
                }
            }

            // Disconnect all clients
            Log.Information("Disconnecting all clients...");
            await _networkManager.DisconnectAllAsync();

            Log.Information("Server shutdown sequence completed");
        }

        public void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _tcpListener?.Stop();
                _messageWriter.Complete();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during GameServer disposal");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
            }
        }

        // Public properties for monitoring
        public long TotalConnectionsAccepted => _totalConnectionsAccepted;
        public long TotalMessagesProcessed => _totalMessagesProcessed;
        public int CurrentConnections => _networkManager.ConnectionCount;
        public TimeSpan Uptime => DateTime.UtcNow - _serverStartTime;
        public bool IsRunning => _tcpListener != null && _cancellationTokenSource?.Token.IsCancellationRequested == false;
    }
}