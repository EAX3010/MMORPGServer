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

                packet.Seek(40);
                string name = packet.ReadString(32);
                packet.Seek(136);
                short body = (short)packet.ReadUInt16();
                short Class = (short)packet.ReadUInt16();
                uint createdAtFingerPrint = packet.ReadUInt32();
                string createdAtMacAddress = packet.ReadString(32);

                client.Player = Player.Create(client.PlayerId, name, body, (ClassType)Class,
                    GameSystemsManager.TwinCity, createdAtFingerPrint, createdAtMacAddress);
                _ = client.Player.UpdateAllotPoints();

                bool success = await GameSystemsManager.PlayerManager?.SavePlayerAsync(client.Player)!;
                if (success)
                {
                    Log.Information("New client created: {Name}, Body: {Body}, Class: {Class}, FingerPrint: {FingerPrint}, MacAddress: {MacAddress}",
                     client.Player.Name, client.Player.Body, client.Player.Class, client.Player.CreatedFingerPrint, client.Player.CreatedAtMacAddress);

                    if (client.Player == null)
                    {
                        Log.Error("Failed to create player for client {ClientId}", client.ClientId);
                        return;
                    }
                    await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
                    await client.SendPacketAsync(PacketFactory.CreateHeroInfoPacket(client.Player));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling CMsgNewClient packet");
            }
        }
    }
}
