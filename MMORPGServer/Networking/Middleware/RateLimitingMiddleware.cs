using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Core;
using Serilog;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace MMORPGServer.Networking.Middleware
{
    /// <summary>
    /// Middleware for rate limiting and flood protection
    /// </summary>
    public sealed class RateLimitingMiddleware : IPacketMiddleware, IDisposable
    {
        private const int MAX_PACKETS_PER_SECOND = 100;
        private const int RATE_LIMIT_WINDOW_SECONDS = 1;
        private const int FLOOD_DETECTION_WINDOW_MS = 100;
        private const int FLOOD_DETECTION_THRESHOLD = 10;
        private const int MAX_PACKET_TYPES_PER_MINUTE = 50;

        // Per-client rate limiters
        private readonly ConcurrentDictionary<int, ClientRateLimitData> _clientLimiters = new();
        private readonly Timer _cleanupTimer;

        public RateLimitingMiddleware()
        {
            // Cleanup expired rate limiters every 5 minutes
            _cleanupTimer = new Timer(CleanupExpiredLimiters, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            Log.Information("RateLimitingMiddleware initialized");
        }

        public async ValueTask<bool> InvokeAsync(GameClient client, Packet packet, Func<ValueTask> next)
        {
            var limitData = GetOrCreateLimitData(client.ClientId);

            // Check packet rate limit
            using var packetLease = await limitData.PacketRateLimiter.AcquireAsync(1);
            if (!packetLease.IsAcquired)
            {
                Log.Warning("Client {ClientId} exceeded packet rate limit", client.ClientId);
                await client.DisconnectAsync("Packet rate limit exceeded");
                return false;
            }

            // Check flood detection
            if (IsFlooding(limitData))
            {
                Log.Warning("Client {ClientId} is flooding, disconnecting", client.ClientId);
                await client.DisconnectAsync("Flood detected");
                return false;
            }

            // Check packet type diversity (anti-fuzzing)
            if (IsSuspiciousPacketDiversity(limitData, packet))
            {
                Log.Warning("Client {ClientId} sending too many different packet types - possible fuzzing",
                    client.ClientId);
                await client.DisconnectAsync("Suspicious packet diversity");
                return false;
            }

            // Record packet and continue
            RecordPacket(limitData, packet);
            await next();
            return true;
        }

        private ClientRateLimitData GetOrCreateLimitData(int clientId)
        {
            return _clientLimiters.GetOrAdd(clientId, _ => new ClientRateLimitData());
        }

        private bool IsFlooding(ClientRateLimitData limitData)
        {
            var now = DateTime.UtcNow;

            lock (limitData.RecentPacketTimes)
            {
                // Remove old entries
                while (limitData.RecentPacketTimes.Count > 0 &&
                       (now - limitData.RecentPacketTimes.Peek()).TotalMilliseconds > FLOOD_DETECTION_WINDOW_MS)
                {
                    limitData.RecentPacketTimes.Dequeue();
                }

                return limitData.RecentPacketTimes.Count > FLOOD_DETECTION_THRESHOLD;
            }
        }

        private bool IsSuspiciousPacketDiversity(ClientRateLimitData limitData, Packet packet)
        {
            lock (limitData.RecentPacketTypes)
            {
                // Reset packet types every minute
                var now = DateTime.UtcNow;
                if (now - limitData.LastPacketTypeReset > TimeSpan.FromMinutes(1))
                {
                    limitData.RecentPacketTypes.Clear();
                    limitData.LastPacketTypeReset = now;
                }

                limitData.RecentPacketTypes.Add(packet.Type);
                return limitData.RecentPacketTypes.Count > MAX_PACKET_TYPES_PER_MINUTE;
            }
        }

        private void RecordPacket(ClientRateLimitData limitData, Packet packet)
        {
            var now = DateTime.UtcNow;

            lock (limitData.RecentPacketTimes)
            {
                limitData.RecentPacketTimes.Enqueue(now);
            }

            limitData.LastActivity = now;
        }

        private void CleanupExpiredLimiters(object? state)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-10); // Remove data older than 10 minutes
            var expiredClients = new List<int>();

            foreach (var kvp in _clientLimiters)
            {
                if (kvp.Value.LastActivity < cutoff)
                {
                    expiredClients.Add(kvp.Key);
                }
            }

            foreach (var clientId in expiredClients)
            {
                if (_clientLimiters.TryRemove(clientId, out var limitData))
                {
                    limitData.Dispose();
                    Log.Debug("Cleaned up rate limit data for expired client {ClientId}", clientId);
                }
            }

            if (expiredClients.Count > 0)
            {
                Log.Debug("Cleaned up rate limit data for {Count} expired clients", expiredClients.Count);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();

            foreach (var limitData in _clientLimiters.Values)
            {
                limitData.Dispose();
            }

            _clientLimiters.Clear();
            Log.Information("RateLimitingMiddleware disposed");
        }

        private sealed class ClientRateLimitData : IDisposable
        {
            public readonly TokenBucketRateLimiter PacketRateLimiter;
            public readonly Queue<DateTime> RecentPacketTimes = new();
            public readonly HashSet<GamePackets> RecentPacketTypes = new();
            public DateTime LastPacketTypeReset = DateTime.UtcNow;
            public DateTime LastActivity = DateTime.UtcNow;

            public ClientRateLimitData()
            {
                PacketRateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = MAX_PACKETS_PER_SECOND,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(RATE_LIMIT_WINDOW_SECONDS),
                    TokensPerPeriod = MAX_PACKETS_PER_SECOND,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                });
            }

            public void Dispose()
            {
                PacketRateLimiter?.Dispose();
            }
        }
    }
}