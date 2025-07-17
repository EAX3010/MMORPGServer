using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Security;
using MMORPGServer.Services;
using Serilog;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace MMORPGServer.Networking.Server
{
    public sealed class GameServer : IDisposable
    {
        private readonly NetworkManager _networkManager;
        private readonly PacketHandlerService _packetHandler;

        // Per-client channels for sequential processing
        private readonly ConcurrentDictionary<int, Channel<ClientMessage>> _clientChannels = new();
        private readonly ConcurrentDictionary<int, Task> _clientProcessingTasks = new();

        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _acceptTask;
        private int _nextClientId = 1;
        private long _totalConnectionsAccepted = 0;
        private long _totalMessagesProcessed = 0;
        private DateTime _serverStartTime;

        public GameServer(NetworkManager networkManager, PacketHandlerService packetHandler)
        {
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _packetHandler = packetHandler ?? throw new ArgumentNullException(nameof(packetHandler));

            Log.Debug("GameServer initialized with direct client channel processing");
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

                    // Create dedicated channel for this client FIRST
                    var clientChannel = CreateClientChannel(clientId, cancellationToken);

                    // Create cryptographic services directly
                    var dhKeyExchange = new DiffieHellmanKeyExchange();
                    var cryptographer = new TQCast5Cryptographer();

                    // Create GameClient with direct channel writer
                    var gameClient = new GameClient(
                        clientId,
                        tcpClient,
                        dhKeyExchange,
                        cryptographer,
                        clientChannel.Writer  // Direct to client channel!
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
                            Log.Error(ex, "Error in client {ClientId} processing task", clientId);
                        }
                        finally
                        {
                            // Ensure client is removed when done
                            RemoveClient(clientId);
                        }
                    }, cancellationToken);

                    Log.Information("Client #{ClientId} connected from {ClientEndpoint} (Total: {CurrentConnections}/{MaxConnections})",
                        clientId, clientEndpoint, _networkManager.ConnectionCount, maxClients);

                    // Log milestone connections
                    if (_totalConnectionsAccepted > 0 && _totalConnectionsAccepted % 100 == 0)
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
                    Log.Error(ex, "Error accepting new client connection");
                    await Task.Delay(1000, cancellationToken); // Brief delay to prevent rapid error loops
                }
            }

            Log.Debug("Client acceptance loop stopped");
        }

        private Channel<ClientMessage> CreateClientChannel(int clientId, CancellationToken cancellationToken)
        {
            // Create unbounded channel for this client
            var channel = Channel.CreateUnbounded<ClientMessage>();
            _clientChannels[clientId] = channel;

            // Start processing task for this client
            var processingTask = ProcessClientChannelAsync(clientId, channel.Reader, cancellationToken);
            _clientProcessingTasks[clientId] = processingTask;

            Log.Debug("Created dedicated channel for client {ClientId}", clientId);

            return channel;
        }

        private async Task ProcessClientChannelAsync(int clientId, ChannelReader<ClientMessage> reader, CancellationToken cancellationToken)
        {
            Log.Debug("Started message processing for client {ClientId}", clientId);

            try
            {
                await foreach (var message in reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        message.Packet.Seek(4);

                        // Process the packet (sequential per client, parallel across clients)
                        await _packetHandler.HandlePacketAsync(message.Client, message.Packet);

                        Interlocked.Increment(ref _totalMessagesProcessed);

                        // Log message processing milestones
                        if (_totalMessagesProcessed > 0 && _totalMessagesProcessed % 10000 == 0)
                        {
                            Log.Debug("Processed {TotalMessages} total messages across all clients", _totalMessagesProcessed);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Debug("Packet processing cancelled for client {ClientId}", clientId);
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        Log.Debug("Client {ClientId} disposed during packet processing", clientId);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error processing packet from client {ClientId} (Player: {PlayerName}, Type: {PacketType})",
                            message.Client.ClientId, message.Client.Player?.Name ?? "N/A", message.Packet.Type);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Debug("Message processing channel was closed for client {ClientId}", clientId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in message processing loop for client {ClientId}", clientId);
            }

            Log.Debug("Message processing stopped for client {ClientId}", clientId);
        }

        public void RemoveClient(int clientId)
        {
            _networkManager.RemoveClient(clientId);

            // Clean up client's dedicated channel and processing task
            if (_clientChannels.TryRemove(clientId, out var channel))
            {
                channel.Writer.Complete();
                Log.Debug("Completed message channel for client {ClientId}", clientId);
            }

            if (_clientProcessingTasks.TryRemove(clientId, out var task))
            {
                // Don't await here - let it finish naturally
                _ = task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Log.Error(t.Exception, "Client {ClientId} processing task faulted after completion", clientId);
                    }
                    Log.Debug("Processing task officially completed for client {ClientId}", clientId);
                }, TaskScheduler.Default);
            }
        }

        public async ValueTask BroadcastPacketAsync(ReadOnlyMemory<byte> packetData, int excludeClientId = 0)
        {
            int connectedClients = _networkManager.ConnectionCount;
            if (connectedClients > 0)
            {
                await _networkManager.BroadcastAsync(packetData, excludeClientId);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            TimeSpan uptime = DateTime.UtcNow - _serverStartTime;

            Log.Warning("Initiating server shutdown...");

            // Cancel all operations
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            Log.Debug("Stopping TCP listener...");
            _tcpListener?.Stop();

            // Complete all client channels
            Log.Debug("Completing all client message channels...");
            foreach (var channel in _clientChannels.Values)
            {
                channel.Writer.TryComplete();
            }

            // Wait for background tasks
            var tasks = new List<Task>();
            if (_acceptTask != null)
                tasks.Add(_acceptTask);

            tasks.AddRange(_clientProcessingTasks.Values);

            if (tasks.Count > 0)
            {
                Log.Debug("Waiting for {TaskCount} background tasks to complete...", tasks.Count);
                try
                {
                    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                }
                catch (TimeoutException)
                {
                    Log.Warning("Some background tasks did not complete within the 10-second timeout");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while waiting for background tasks to complete");
                }
            }

            // Disconnect all clients
            Log.Debug("Disconnecting all remaining clients...");
            await _networkManager.DisconnectAllAsync();

            // Clear collections
            _clientChannels.Clear();
            _clientProcessingTasks.Clear();

            Log.Information(
                "\n========================================\n" +
                "         Server Shutdown Complete        \n" +
                "========================================\n" +
                "Total Uptime: {Uptime}\n" +
                "Total Connections: {TotalConnections}\n" +
                "Messages Processed: {TotalMessages}\n" +
                "========================================",
                uptime.ToString(@"dd\.hh\:mm\:ss"),
                _totalConnectionsAccepted,
                _totalMessagesProcessed
            );
        }

        public void Dispose()
        {
            try
            {
                if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                }
                _tcpListener?.Stop();

                // Complete all client channels
                foreach (var channel in _clientChannels.Values)
                {
                    channel.Writer.TryComplete();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during GameServer disposal");
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _clientChannels.Clear();
                _clientProcessingTasks.Clear();
            }
        }

        // Public properties for monitoring
        public long TotalConnectionsAccepted => _totalConnectionsAccepted;
        public long TotalMessagesProcessed => _totalMessagesProcessed;
        public int CurrentConnections => _networkManager.ConnectionCount;
        public int ActiveClientChannels => _clientChannels.Count;
        public TimeSpan Uptime => DateTime.UtcNow - _serverStartTime;
        public bool IsRunning => _tcpListener != null && _cancellationTokenSource?.IsCancellationRequested == false;
    }
}
