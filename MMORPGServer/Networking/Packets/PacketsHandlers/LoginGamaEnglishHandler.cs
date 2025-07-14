using MMORPGServer.Common.Enums;
using MMORPGServer.Entities;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Services;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{
    public record LoginGamaEnglishData(int Id, int State);
    public sealed class LoginGamaEnglishHandler
    {
        [PacketHandler(GamePackets.LoginGamaEnglish)]
        public static async ValueTask HandleAsync(GameClient client, Packet packet)
        {
            try
            {
                uint[] outputDecrypted = GameSystemsManager.TransferCipher!.Decrypt([(uint)packet.ReadInt32(), (uint)packet.ReadInt32()]);
                LoginGamaEnglishData data = new((int)outputDecrypted[0], (int)outputDecrypted[1]);

                Log.Debug("LoginGamaEnglish decrypted for ClientId {ClientId} -> PlayerId: {PlayerId}, State: {State}", client.ClientId, data.Id, data.State);

                client.PlayerId = data.Id;
                Player? player = await GameSystemsManager.GameWorld.PlayerManager.LoadPlayerAsync(data.Id);

                if (player != null)
                {
                    client.Player = player;
                    client.Player.ClientId = client.ClientId;

                    await GameSystemsManager.GameWorld.SpawnPlayerAsync(client.Player, client.Player.MapId);
                    await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
                    await client.SendPacketAsync(PacketFactory.CreateHeroInfoPacket(client.Player));
                    Log.Information("Player {PlayerName} (ID: {PlayerId}) has logged in successfully for ClientId {ClientId}", player.Name, player.Id, client.ClientId);
                }
                else
                {
                    Log.Information("Player with ID {PlayerId} not found for ClientId {ClientId}, requesting new character creation", data.Id, client.ClientId);
                    await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "NEW_ROLE", ChatType.Dialog, 0));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling LoginGamaEnglish packet for ClientId {ClientId}", client.ClientId);
            }
        }
    }
}
