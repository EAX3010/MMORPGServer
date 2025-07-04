﻿using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
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
        public static async ValueTask HandleAsync(GameClient client, IPacket packet)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(packet);


            uint[] outputDecrypted = GameSystemsManager.TransferCipher!.Decrypt([(uint)packet.ReadInt32(), (uint)packet.ReadInt32()]);

            LoginGamaEnglishData data = new((int)outputDecrypted[0], (int)outputDecrypted[1]);



            Log.Information("LoginGamaEnglish decrypted UID: {uid}, State: {state}", data.Id, data.State);
            client.Player = await GameSystemsManager.PlayerManager.LoadPlayerAsync(data.Id, client.ClientId);
            if (client.Player != null)
            {
                _ = await GameSystemsManager.GameWorld.SpawnPlayerAsync(client.Player, 1002);
                await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
                await client.SendPacketAsync(PacketFactory.CreateHeroInfoPacket(client.Player));
            }
            else
            {
                await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "NEW_ROLE", ChatType.Dialog, 0));
            }
        }
    }

}
