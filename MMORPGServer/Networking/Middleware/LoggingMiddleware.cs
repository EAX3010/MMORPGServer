using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Core;
using Serilog;
using System.Collections.Concurrent;

namespace MMORPGServer.Networking.Middleware
{
    /// <summary>
    /// Middleware for detailed packet logging (debug and monitoring purposes)
    /// </summary>
    public sealed class LoggingMiddleware : IPacketMiddleware
    {
        private readonly bool _logAllPackets;
        private readonly HashSet<GamePackets> _loggedPacketTypes;
        private readonly bool _logPacketContent;
        private readonly bool _logClientInfo;
        private readonly ConcurrentDictionary<GamePackets, long> _packetCounts = new();

        public LoggingMiddleware(
            bool logAllPackets = false,
            HashSet<GamePackets>? specificPackets = null,
            bool logPacketContent = false,
            bool logClientInfo = true)
        {
            _logAllPackets = logAllPackets;
            _loggedPacketTypes = specificPackets ?? new HashSet<GamePackets>();
            _logPacketContent = logPacketContent;
            _logClientInfo = logClientInfo;

            Log.Information("LoggingMiddleware initialized (LogAll: {LogAll}, Specific: {SpecificCount}, Content: {Content})",
                _logAllPackets, _loggedPacketTypes.Count, _logPacketContent);
        }

        public async ValueTask<bool> InvokeAsync(GameClient client, Packet packet, Func<ValueTask> next)
        {
            bool shouldLog = _logAllPackets || _loggedPacketTypes.Contains(packet.Type);

            if (shouldLog)
            {
                LogPacketReceived(client, packet);
            }

            // Track packet counts for periodic reporting
            _packetCounts.AddOrUpdate(packet.Type, 1, (_, count) => count + 1);

            try
            {
                await next();

                if (shouldLog)
                {
                    LogPacketProcessed(client, packet, success: true);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (shouldLog || _logAllPackets)
                {
                    LogPacketProcessed(client, packet, success: false, exception: ex);
                }
                throw; // Re-throw to maintain exception handling
            }
        }

        private void LogPacketReceived(GameClient client, Packet packet)
        {
            var logMessage = "Received packet {PacketType} from client {ClientId}";
            var logParams = new List<object> { packet.Type, client.ClientId };

            if (_logClientInfo)
            {
                logMessage += " (IP: {ClientIP}, State: {ClientState}";
                logParams.AddRange(new object[] { client.IPAddress ?? "Unknown", client.State });

                if (client.Player != null)
                {
                    logMessage += ", Player: {PlayerName}";
                    logParams.Add(client.Player.Name ?? "Unknown");
                }

                logMessage += ")";
            }

            logMessage += " - Size: {PacketSize} bytes";
            logParams.Add(packet.Length);

            if (_logPacketContent && packet.Length > 0)
            {
                logMessage += ", Content: {PacketContent}";
                logParams.Add(GetGenericPacketSummary(packet));
            }

            Log.Debug(logMessage, logParams.ToArray());
        }

        private void LogPacketProcessed(GameClient client, Packet packet, bool success, Exception? exception = null)
        {
            if (success)
            {
                Log.Debug("Successfully processed packet {PacketType} from client {ClientId}",
                    packet.Type, client.ClientId);
            }
            else
            {
                Log.Error(exception, "Failed to process packet {PacketType} from client {ClientId}: {ErrorMessage}",
                    packet.Type, client.ClientId, exception?.Message ?? "Unknown error");
            }
        }


        private string GetGenericPacketSummary(Packet packet)
        {
            // For unknown packet types, just show first few bytes as hex
            var data = packet.Data;
            if (data == null || data.Length == 0)
                return "[Empty packet]";

            var previewLength = Math.Min(8, data.Length);

            // Use stackalloc for small hex conversion (more efficient)
            Span<char> hexChars = stackalloc char[previewLength * 3]; // 2 hex chars + 1 space per byte
            int pos = 0;

            for (int i = 0; i < previewLength; i++)
            {
                if (i > 0)
                    hexChars[pos++] = ' ';

                var hexValue = data[i].ToString("X2");
                hexChars[pos++] = hexValue[0];
                hexChars[pos++] = hexValue[1];
            }

            var result = new string(hexChars.Slice(0, pos));

            if (data.Length > previewLength)
                result += "...";

            return $"Hex: {result}";
        }

        /// <summary>
        /// Enable logging for specific packet types at runtime
        /// </summary>
        public void EnableLoggingForPacket(GamePackets packetType)
        {
            _loggedPacketTypes.Add(packetType);
            Log.Information("Enabled logging for packet type {PacketType}", packetType);
        }

        /// <summary>
        /// Disable logging for specific packet types at runtime
        /// </summary>
        public void DisableLoggingForPacket(GamePackets packetType)
        {
            _loggedPacketTypes.Remove(packetType);
            Log.Information("Disabled logging for packet type {PacketType}", packetType);
        }

        /// <summary>
        /// Get current packet counts for monitoring
        /// </summary>
        public IReadOnlyDictionary<GamePackets, long> GetPacketCounts()
        {
            return _packetCounts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Reset packet counts
        /// </summary>
        public void ResetPacketCounts()
        {
            _packetCounts.Clear();
            Log.Debug("Packet counts reset");
        }

        /// <summary>
        /// Log current packet statistics
        /// </summary>
        public void LogPacketStatistics()
        {
            if (_packetCounts.IsEmpty)
            {
                Log.Information("No packets have been logged yet");
                return;
            }

            Log.Information("=== Packet Logging Statistics ===");

            var sortedCounts = _packetCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(10);

            foreach (var (packetType, count) in sortedCounts)
            {
                Log.Information("  {PacketType}: {Count} packets", packetType, count);
            }

            var totalPackets = _packetCounts.Values.Sum();
            Log.Information("Total logged packets: {Total}", totalPackets);
        }
    }
}