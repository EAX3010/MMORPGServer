using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Core;
using Serilog;

namespace MMORPGServer.Networking.Middleware
{
    /// <summary>
    /// Middleware for authentication and authorization checks
    /// </summary>
    public sealed class AuthenticationMiddleware : IPacketMiddleware
    {
        private static readonly HashSet<GamePackets> UnauthenticatedAllowedPackets = new()
        {

        };

        private static readonly HashSet<GamePackets> PlayerManagementPackets = new()
        {

        };

        public AuthenticationMiddleware()
        {
            Log.Information("AuthenticationMiddleware initialized");
        }

        public async ValueTask<bool> InvokeAsync(GameClient client, Packet packet, Func<ValueTask> next)
        {
            // Allow certain packets for unauthenticated clients
            //if (client.State != ClientState.Connected && !UnauthenticatedAllowedPackets.Contains(packet.Type))
            //{
            //    Log.Warning("Client {ClientId} sent packet {PacketType} before authentication",
            //        client.ClientId, packet.Type);
            //    await client.DisconnectAsync("Unauthenticated packet");
            //    return false;
            //}

            // For authenticated clients, check if they have a valid player
            //if (client.State == ClientState.Connected &&
            //    client.Player == null &&
            //    !UnauthenticatedAllowedPackets.Contains(packet.Type) &&
            //    !PlayerManagementPackets.Contains(packet.Type))
            //{
            //    Log.Warning("Authenticated client {ClientId} has no player but sent {PacketType}",
            //        client.ClientId, packet.Type);
            //    await client.DisconnectAsync("No player assigned");
            //    return false;
            //}

            // Additional checks for specific packet types
            if (!await ValidatePacketPermissions(client, packet))
            {
                return false;
            }

            await next();
            return true;
        }

        private async ValueTask<bool> ValidatePacketPermissions(GameClient client, Packet packet)
        {
            // Check if player has required permissions for certain packets
            switch (packet.Type)
            {

            }

            return true;
        }
    }
}