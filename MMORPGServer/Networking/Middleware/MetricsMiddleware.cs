using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets;
using Serilog;
using System.Collections.Concurrent;

namespace MMORPGServer.Networking.Middleware
{
    /// <summary>
    /// Middleware for logging and metrics collection
    /// </summary>
    public sealed class MetricsMiddleware : IPacketMiddleware, IDisposable
    {
        private readonly ConcurrentDictionary<GamePackets, PacketMetrics> _packetMetrics = new();
        private readonly ConcurrentDictionary<int, ClientMetrics> _clientMetrics = new();
        private readonly Timer _reportTimer;
        private long _totalPacketsProcessed;
        private long _totalPacketsFailed;

        public MetricsMiddleware()
        {
            // Report metrics every 5 minutes
            _reportTimer = new Timer(ReportMetrics, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            Log.Information("MetricsMiddleware initialized");
        }

        public async ValueTask<bool> InvokeAsync(GameClient client, Packet packet, Func<ValueTask> next)
        {
            var startTime = DateTime.UtcNow;
            var clientMetrics = GetOrCreateClientMetrics(client.ClientId);
            var packetMetrics = GetOrCreatePacketMetrics(packet.Type);

            try
            {
                await next();

                // Record successful packet processing
                RecordSuccessfulPacket(clientMetrics, packetMetrics, startTime);
                Interlocked.Increment(ref _totalPacketsProcessed);
                return true;
            }
            catch (Exception ex)
            {
                // Record failed packet processing
                RecordFailedPacket(clientMetrics, packetMetrics, ex);
                Interlocked.Increment(ref _totalPacketsFailed);

                Log.Error(ex, "Packet processing failed for client {ClientId}, packet {PacketType}",
                    client.ClientId, packet.Type);
                return false;
            }
        }

        private ClientMetrics GetOrCreateClientMetrics(int clientId)
        {
            return _clientMetrics.GetOrAdd(clientId, _ => new ClientMetrics { ClientId = clientId });
        }

        private PacketMetrics GetOrCreatePacketMetrics(GamePackets packetType)
        {
            return _packetMetrics.GetOrAdd(packetType, _ => new PacketMetrics { PacketType = packetType });
        }

        private void RecordSuccessfulPacket(ClientMetrics clientMetrics, PacketMetrics packetMetrics, DateTime startTime)
        {
            var processingTime = DateTime.UtcNow - startTime;
            var processingTimeMs = processingTime.TotalMilliseconds;

            // Update client metrics
            lock (clientMetrics)
            {
                clientMetrics.TotalPackets++;
                clientMetrics.TotalProcessingTime += processingTimeMs;
                clientMetrics.LastActivity = DateTime.UtcNow;

                if (processingTimeMs > clientMetrics.MaxProcessingTime)
                    clientMetrics.MaxProcessingTime = processingTimeMs;
            }

            // Update packet type metrics
            lock (packetMetrics)
            {
                packetMetrics.TotalCount++;
                packetMetrics.TotalProcessingTime += processingTimeMs;

                if (processingTimeMs > packetMetrics.MaxProcessingTime)
                    packetMetrics.MaxProcessingTime = processingTimeMs;

                if (processingTimeMs < packetMetrics.MinProcessingTime || packetMetrics.MinProcessingTime == 0)
                    packetMetrics.MinProcessingTime = processingTimeMs;
            }

            // Log slow packet processing
            if (processingTimeMs > 5) // Log if packet took more than 100ms
            {
                Log.Warning("Slow packet processing: {PacketType} from client {ClientId} took {ProcessingTime:F2}ms",
                    packetMetrics.PacketType, clientMetrics.ClientId, processingTimeMs);
            }
        }

        private void RecordFailedPacket(ClientMetrics clientMetrics, PacketMetrics packetMetrics, Exception exception)
        {
            // Update client metrics
            lock (clientMetrics)
            {
                clientMetrics.FailedPackets++;
                clientMetrics.LastActivity = DateTime.UtcNow;

                // Track error types
                var errorType = exception.GetType().Name;
                clientMetrics.ErrorTypes.TryAdd(errorType, 0);
                clientMetrics.ErrorTypes[errorType]++;
            }

            // Update packet type metrics
            lock (packetMetrics)
            {
                packetMetrics.FailedCount++;

                // Track error types for this packet type
                var errorType = exception.GetType().Name;
                packetMetrics.ErrorTypes.TryAdd(errorType, 0);
                packetMetrics.ErrorTypes[errorType]++;
            }
        }

        private void ReportMetrics(object? state)
        {
            var totalProcessed = Interlocked.Read(ref _totalPacketsProcessed);
            var totalFailed = Interlocked.Read(ref _totalPacketsFailed);

            if (totalProcessed == 0 && totalFailed == 0)
                return;

            Log.Information("=== Packet Processing Metrics (last 5 minutes) ===");
            Log.Information("Total packets: {Processed} processed, {Failed} failed", totalProcessed, totalFailed);

            if (totalProcessed > 0)
            {
                var successRate = (double)totalProcessed / (totalProcessed + totalFailed) * 100;
                Log.Information("Success rate: {SuccessRate:F2}%", successRate);
            }

            ReportPacketTypeMetrics();
            ReportClientMetrics();
            ReportErrorSummary();

            // Reset counters
            ResetMetrics();
        }

        private void ReportPacketTypeMetrics()
        {
            var sortedPacketMetrics = _packetMetrics.Values
                .Where(m => m.TotalCount > 0)
                .OrderByDescending(m => m.TotalCount)
                .Take(10);

            Log.Information("Top packet types by volume:");
            foreach (var metrics in sortedPacketMetrics)
            {
                var avgTime = metrics.TotalCount > 0 ? metrics.TotalProcessingTime / metrics.TotalCount : 0;
                var failureRate = metrics.TotalCount > 0 ? (double)metrics.FailedCount / metrics.TotalCount * 100 : 0;

                Log.Information("  {PacketType}: {Count} packets, {AvgTime:F2}ms avg, {MaxTime:F2}ms max, {FailureRate:F1}% failure",
                    metrics.PacketType, metrics.TotalCount, avgTime, metrics.MaxProcessingTime, failureRate);
            }

            // Report slowest packet types
            var slowestPackets = _packetMetrics.Values
                .Where(m => m.TotalCount > 0)
                .OrderByDescending(m => m.TotalProcessingTime / m.TotalCount)
                .Take(5);

            if (slowestPackets.Any())
            {
                Log.Information("Slowest packet types by average processing time:");
                foreach (var metrics in slowestPackets)
                {
                    var avgTime = metrics.TotalProcessingTime / metrics.TotalCount;
                    Log.Information("  {PacketType}: {AvgTime:F2}ms average ({Count} packets)",
                        metrics.PacketType, avgTime, metrics.TotalCount);
                }
            }
        }

        private void ReportClientMetrics()
        {
            var activeClients = _clientMetrics.Values
                .Where(m => m.TotalPackets > 0)
                .OrderByDescending(m => m.TotalPackets)
                .Take(5);

            if (activeClients.Any())
            {
                Log.Information("Most active clients:");
                foreach (var metrics in activeClients)
                {
                    var avgTime = metrics.TotalPackets > 0 ? metrics.TotalProcessingTime / metrics.TotalPackets : 0;
                    var failureRate = metrics.TotalPackets > 0 ? (double)metrics.FailedPackets / metrics.TotalPackets * 100 : 0;

                    Log.Information("  Client {ClientId}: {Count} packets, {AvgTime:F2}ms avg, {FailureRate:F1}% failure",
                        metrics.ClientId, metrics.TotalPackets, avgTime, failureRate);
                }
            }

            // Report clients with high failure rates
            var problematicClients = _clientMetrics.Values
                .Where(m => m.TotalPackets > 10 && (double)m.FailedPackets / m.TotalPackets > 0.1)
                .OrderByDescending(m => (double)m.FailedPackets / m.TotalPackets)
                .Take(3);

            if (problematicClients.Any())
            {
                Log.Warning("Clients with high failure rates:");
                foreach (var metrics in problematicClients)
                {
                    var failureRate = (double)metrics.FailedPackets / metrics.TotalPackets * 100;
                    Log.Warning("  Client {ClientId}: {FailureRate:F1}% failure rate ({Failed}/{Total})",
                        metrics.ClientId, failureRate, metrics.FailedPackets, metrics.TotalPackets);
                }
            }
        }

        private void ReportErrorSummary()
        {
            var allErrors = new Dictionary<string, int>();

            // Aggregate errors from all packet types
            foreach (var packetMetrics in _packetMetrics.Values)
            {
                foreach (var error in packetMetrics.ErrorTypes)
                {
                    allErrors.TryAdd(error.Key, 0);
                    allErrors[error.Key] += error.Value;
                }
            }

            if (allErrors.Any())
            {
                Log.Information("Error summary:");
                var sortedErrors = allErrors.OrderByDescending(kvp => kvp.Value).Take(5);
                foreach (var error in sortedErrors)
                {
                    Log.Information("  {ErrorType}: {Count} occurrences", error.Key, error.Value);
                }
            }
        }

        private void ResetMetrics()
        {
            Interlocked.Exchange(ref _totalPacketsProcessed, 0);
            Interlocked.Exchange(ref _totalPacketsFailed, 0);

            foreach (var metrics in _packetMetrics.Values)
            {
                lock (metrics)
                {
                    metrics.TotalCount = 0;
                    metrics.FailedCount = 0;
                    metrics.TotalProcessingTime = 0;
                    metrics.MaxProcessingTime = 0;
                    metrics.MinProcessingTime = 0;
                    metrics.ErrorTypes.Clear();
                }
            }

            foreach (var metrics in _clientMetrics.Values)
            {
                lock (metrics)
                {
                    metrics.TotalPackets = 0;
                    metrics.FailedPackets = 0;
                    metrics.TotalProcessingTime = 0;
                    metrics.MaxProcessingTime = 0;
                    metrics.ErrorTypes.Clear();
                }
            }
        }

        public void Dispose()
        {
            _reportTimer?.Dispose();
            _packetMetrics.Clear();
            _clientMetrics.Clear();
            Log.Information("MetricsMiddleware disposed");
        }

        private sealed class PacketMetrics
        {
            public GamePackets PacketType;
            public long TotalCount;
            public long FailedCount;
            public double TotalProcessingTime;
            public double MaxProcessingTime;
            public double MinProcessingTime;
            public readonly ConcurrentDictionary<string, int> ErrorTypes = new();
        }

        private sealed class ClientMetrics
        {
            public int ClientId;
            public long TotalPackets;
            public long FailedPackets;
            public double TotalProcessingTime;
            public double MaxProcessingTime;
            public DateTime LastActivity = DateTime.UtcNow;
            public readonly ConcurrentDictionary<string, int> ErrorTypes = new();
        }
    }
}