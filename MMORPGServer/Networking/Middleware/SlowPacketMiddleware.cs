using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets;
using Serilog;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace MMORPGServer.Networking.Middleware
{
    /// <summary>
    /// Middleware for detecting and handling slow packet processing
    /// </summary>
    public sealed class SlowPacketMiddleware : IPacketMiddleware, IDisposable
    {
        private const int SLOW_PACKET_THRESHOLD_MS = 100;
        private const int VERY_SLOW_PACKET_THRESHOLD_MS = 500;
        private const int MAX_SLOW_PACKETS_PER_CLIENT = 10;
        private const int SLOW_PACKET_WINDOW_MINUTES = 5;

        private readonly ConcurrentDictionary<int, ClientSlowPacketData> _clientSlowData = new();
        private readonly ConcurrentDictionary<GamePackets, PacketPerformanceStats> _packetStats = new();
        private readonly Timer _reportTimer;
        private readonly Timer _cleanupTimer;

        public SlowPacketMiddleware()
        {
            // Report slow packet statistics every 10 minutes
            _reportTimer = new Timer(ReportSlowPacketStats, null,
                TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

            // Cleanup expired data every 5 minutes
            _cleanupTimer = new Timer(CleanupExpiredData, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            Log.Information("SlowPacketMiddleware initialized (Threshold: {ThresholdMs}ms)", SLOW_PACKET_THRESHOLD_MS);
        }

        public async ValueTask<bool> InvokeAsync(GameClient client, Packet packet, Func<ValueTask> next)
        {
            var stopwatch = Stopwatch.StartNew();
            var clientData = GetOrCreateClientData(client.ClientId);

            try
            {
                await next();
                return true;
            }
            finally
            {
                stopwatch.Stop();
                var processingTime = stopwatch.ElapsedMilliseconds;

                // Record packet processing time
                RecordPacketProcessingTime(packet.Type, processingTime);

                // Handle slow packets
                if (processingTime >= SLOW_PACKET_THRESHOLD_MS)
                {
                    await HandleSlowPacket(client, packet, processingTime, clientData);
                }
            }
        }

        private ClientSlowPacketData GetOrCreateClientData(int clientId)
        {
            return _clientSlowData.GetOrAdd(clientId, _ => new ClientSlowPacketData());
        }

        private void RecordPacketProcessingTime(GamePackets packetType, long processingTimeMs)
        {
            _packetStats.AddOrUpdate(packetType,
                new PacketPerformanceStats { TotalTime = processingTimeMs, Count = 1, MaxTime = processingTimeMs },
                (_, existing) =>
                {
                    existing.TotalTime += processingTimeMs;
                    existing.Count++;
                    if (processingTimeMs > existing.MaxTime)
                        existing.MaxTime = processingTimeMs;
                    return existing;
                });
        }

        private async Task HandleSlowPacket(GameClient client, Packet packet, long processingTimeMs, ClientSlowPacketData clientData)
        {
            var now = DateTime.UtcNow;

            // Clean old slow packet records
            lock (clientData.SlowPacketTimes)
            {
                while (clientData.SlowPacketTimes.Count > 0 &&
                       (now - clientData.SlowPacketTimes.Peek()).TotalMinutes > SLOW_PACKET_WINDOW_MINUTES)
                {
                    clientData.SlowPacketTimes.Dequeue();
                }

                clientData.SlowPacketTimes.Enqueue(now);
            }

            // Log based on severity
            if (processingTimeMs >= VERY_SLOW_PACKET_THRESHOLD_MS)
            {
                Log.Error("VERY SLOW packet processing: {PacketType} from client {ClientId} took {ProcessingTime}ms",
                    packet.Type, client.ClientId, processingTimeMs);

                clientData.VerySlowPacketCount++;
            }
            else
            {
                Log.Warning("Slow packet processing: {PacketType} from client {ClientId} took {ProcessingTime}ms",
                    packet.Type, client.ClientId, processingTimeMs);
            }

            clientData.TotalSlowPackets++;
            clientData.LastSlowPacket = now;

            // Check if client is consistently sending slow packets
            if (clientData.SlowPacketTimes.Count > MAX_SLOW_PACKETS_PER_CLIENT)
            {
                Log.Warning("Client {ClientId} has sent {SlowPacketCount} slow packets in the last {WindowMinutes} minutes - possible DoS attempt",
                    client.ClientId, clientData.SlowPacketTimes.Count, SLOW_PACKET_WINDOW_MINUTES);

                // Optional: Disconnect client for excessive slow packets
                // await client.DisconnectAsync("Excessive slow packet processing");
            }

            // Track packet type that's consistently slow
            if (processingTimeMs >= VERY_SLOW_PACKET_THRESHOLD_MS)
            {
                clientData.SlowPacketTypes.TryAdd(packet.Type, 0);
                clientData.SlowPacketTypes[packet.Type]++;

                if (clientData.SlowPacketTypes[packet.Type] > 5)
                {
                    Log.Error("Client {ClientId} consistently sending slow {PacketType} packets ({Count} times)",
                        client.ClientId, packet.Type, clientData.SlowPacketTypes[packet.Type]);
                }
            }
        }

        private void ReportSlowPacketStats(object? state)
        {
            if (_packetStats.IsEmpty)
                return;

            Log.Information("=== Slow Packet Processing Report ===");

            // Report slowest packet types by average processing time
            var slowestPackets = _packetStats
                .Where(kvp => kvp.Value.Count > 0)
                .Select(kvp => new
                {
                    PacketType = kvp.Key,
                    AverageTime = kvp.Value.TotalTime / kvp.Value.Count,
                    MaxTime = kvp.Value.MaxTime,
                    Count = kvp.Value.Count
                })
                .Where(x => x.AverageTime >= SLOW_PACKET_THRESHOLD_MS)
                .OrderByDescending(x => x.AverageTime)
                .Take(10);

            foreach (var packet in slowestPackets)
            {
                Log.Information("  {PacketType}: Avg {AvgTime}ms, Max {MaxTime}ms, Count {Count}",
                    packet.PacketType, packet.AverageTime, packet.MaxTime, packet.Count);
            }

            // Report clients with most slow packets
            var slowestClients = _clientSlowData
                .Where(kvp => kvp.Value.TotalSlowPackets > 0)
                .Select(kvp => new { ClientId = kvp.Key, Data = kvp.Value })
                .OrderByDescending(x => x.Data.TotalSlowPackets)
                .Take(5);

            if (slowestClients.Any())
            {
                Log.Information("Clients with most slow packets:");
                foreach (var client in slowestClients)
                {
                    Log.Information("  Client {ClientId}: {SlowCount} slow, {VerySlowCount} very slow",
                        client.ClientId, client.Data.TotalSlowPackets, client.Data.VerySlowPacketCount);
                }
            }

            // Reset statistics
            _packetStats.Clear();
            foreach (var clientData in _clientSlowData.Values)
            {
                clientData.TotalSlowPackets = 0;
                clientData.VerySlowPacketCount = 0;
                clientData.SlowPacketTypes.Clear();
            }
        }

        private void CleanupExpiredData(object? state)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-30); // Remove data older than 30 minutes
            var expiredClients = _clientSlowData
                .Where(kvp => kvp.Value.LastSlowPacket < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var clientId in expiredClients)
            {
                _clientSlowData.TryRemove(clientId, out _);
            }

            if (expiredClients.Count > 0)
            {
                Log.Debug("Cleaned up slow packet data for {Count} expired clients", expiredClients.Count);
            }
        }

        public void Dispose()
        {
            _reportTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _clientSlowData.Clear();
            _packetStats.Clear();
            Log.Information("SlowPacketMiddleware disposed");
        }

        private sealed class ClientSlowPacketData
        {
            public readonly Queue<DateTime> SlowPacketTimes = new();
            public readonly ConcurrentDictionary<GamePackets, int> SlowPacketTypes = new();
            public int TotalSlowPackets;
            public int VerySlowPacketCount;
            public DateTime LastSlowPacket = DateTime.UtcNow;
        }

        private sealed class PacketPerformanceStats
        {
            public long TotalTime;
            public long Count;
            public long MaxTime;
        }
    }
}