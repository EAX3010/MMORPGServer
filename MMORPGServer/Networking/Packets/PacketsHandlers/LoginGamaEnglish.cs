using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Entities;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Core;
using MMORPGServer.Networking.Packets.Structures;
using MMORPGServer.Services;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers
{

    [PacketHandler(GamePackets.LoginGamaEnglish)]
    public sealed class LoginGamaEnglish : PacketBaseHandler, IPacketHandler<LoginGamaEnglishData>
    {
        public LoginGamaEnglish(Packet packet) : base(packet) { }

        public override async ValueTask ProcessAsync(GameClient client)
        {
            // Read and decrypt data
            var data = Read();
            if (data == null)
            {
                Log.Warning("Failed to read login data for client {ClientId}", client.ClientId);
                return;
            }

            Log.Debug("LoginGamaEnglish decrypted for ClientId {ClientId} -> PlayerId: {PlayerId}, State: {State}",
                client.ClientId, data.Id, data.State);

            client.PlayerId = data.Id;

            Player? player = await GameRuntime.GameWorld.PlayerManager.LoadPlayerAsync(data.Id);
            if (player != null)
            {
                await HandleExistingPlayerAsync(client, player);
            }
            else
            {
                await HandleNewPlayerRequestAsync(client, data);
            }
        }

        public LoginGamaEnglishData? Read()
        {
            try
            {
                uint[] outputDecrypted = GameRuntime.TransferCipher!.Decrypt([
                    (uint)Packet.ReadInt32(),
                    (uint)Packet.ReadInt32()
                ]);

                var data = new LoginGamaEnglishData((int)outputDecrypted[0], (int)outputDecrypted[1]);

                // Basic validation
                if (data.Id <= 0)
                {
                    Log.Warning("Invalid player ID in login data: {Id}", data.Id);
                    return null;
                }

                return data;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading login data");
                return null;
            }
        }

        private async ValueTask HandleExistingPlayerAsync(GameClient client, Player player)
        {
            client.PlayerContext = player;
            _ = await GameRuntime.GameWorld.AddPlayerAsync(client.Player, client.Player.MapId);

            await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
            await client.SendPacketAsync(PacketFactory.CreateHeroInfoPacket(client.Player));

            Log.Information("Player {PlayerName} (ID: {PlayerId}) has logged in successfully for ClientId {ClientId}",
                player.Name, player.Id, client.ClientId);
        }

        private async ValueTask HandleNewPlayerRequestAsync(GameClient client, LoginGamaEnglishData data)
        {
            Log.Information("Player with ID {PlayerId} not found for ClientId {ClientId}, requesting new character creation",
                data.Id, client.ClientId);
            await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "NEW_ROLE", ChatType.Dialog, 0));
        }


    }
}
