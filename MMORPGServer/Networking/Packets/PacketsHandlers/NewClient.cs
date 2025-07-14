using MMORPGServer.Common.Enums;
using MMORPGServer.Entities;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Services;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    public sealed class NewClient
    {
        [PacketHandler(GamePackets.CMsgNewClient)]
        public static async ValueTask HandleAsync(GameClient client, Packet packet)
        {
            try
            {
                Log.Debug("Handling new client creation for ClientId {ClientId}", client.ClientId);

                packet.Seek(40);
                string name = packet.ReadString(32);
                packet.Seek(136);
                short body = (short)packet.ReadUInt16();
                short Class = (short)packet.ReadUInt16();
                uint createdAtFingerPrint = packet.ReadUInt32();
                string createdAtMacAddress = packet.ReadString(32);

                client.Player = Player.Create(client.PlayerId, name, body, (ClassType)Class,
                    GameSystemsManager.GameWorld.TwinCity, createdAtFingerPrint, createdAtMacAddress);
                _ = client.Player.UpdateAllotPoints();

                bool success = await GameSystemsManager.GameWorld.PlayerManager.SavePlayerAsync(client.Player);
                if (success)
                {
                    Log.Information("New player '{PlayerName}' (ID: {PlayerId}) created for ClientId {ClientId}",
                         client.Player.Name, client.Player.Id, client.ClientId);

                    if (client.Player == null)
                    {
                        Log.Error("Failed to assign created player to client {ClientId}", client.ClientId);
                        return;
                    }

                    Log.Debug("Sending ANSWER_OK and HeroInfo to client {ClientId}", client.ClientId);
                    await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
                    await client.SendPacketAsync(PacketFactory.CreateHeroInfoPacket(client.Player));
                }
                else
                {
                    Log.Warning("Failed to save new player for ClientId {ClientId}", client.ClientId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling CMsgNewClient packet for ClientId {ClientId}", client.ClientId);
            }
        }
    }
}
