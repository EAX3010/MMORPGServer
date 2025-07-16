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

    [PacketHandler(GamePackets.CMsgRegister)]
    public sealed class Register : PacketBaseHandler, IPacketHandler<RegisterData>
    {
        public Register(Packet packet) : base(packet) { }

        public override async ValueTask<bool> ValidateAsync(GameClient client)
        {
            if (!await base.ValidateAsync(client))
                return false;

            if (client.PlayerId <= 0)
            {
                Log.Warning("NewClient packet received from client {ClientId} with no PlayerId set", client.ClientId);
                return false;
            }

            return true;
        }

        public override async ValueTask ProcessAsync(GameClient client)
        {
            Log.Debug("Handling new client creation for ClientId {ClientId}", client.ClientId);

            // Read packet data
            var data = Read();
            if (data == null)
            {
                Log.Warning("Failed to read new client data for client {ClientId}", client.ClientId);
                return;
            }

            // Create player
            client.PlayerContext = Player.Create(
                client.PlayerId,
                data.Name,
                data.Body,
                (ClassType)data.Class,
                GameRuntime.GameWorld.TwinCity,
                data.CreatedAtFingerPrint,
                data.CreatedAtMacAddress);

            _ = client.Player?.UpdateAllotPoints();

            // Save player
            bool success = await GameRuntime.GameWorld.PlayerManager.SavePlayerAsync(client.Player!);

            if (success)
            {
                await HandlePlayerCreationSuccessAsync(client);
            }
            else
            {
                await HandlePlayerCreationFailureAsync(client);
            }
        }

        public RegisterData? Read()
        {
            try
            {
                Packet.Seek(40);
                string name = Packet.ReadString(32);

                Packet.Seek(136);
                short body = (short)Packet.ReadUInt16();
                short classType = (short)Packet.ReadUInt16();
                uint createdAtFingerPrint = Packet.ReadUInt32();
                string createdAtMacAddress = Packet.ReadString(32);

                // Basic validation
                if (string.IsNullOrWhiteSpace(name) || name.Length < 2)
                {
                    Log.Warning("Invalid player name: {Name}", name);
                    return null;
                }

                if (!Enum.IsDefined(typeof(ClassType), classType))
                {
                    Log.Warning("Invalid class type: {Class}", classType);
                    return null;
                }

                return new RegisterData(name, body, classType, createdAtFingerPrint, createdAtMacAddress);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error reading new client data");
                return null;
            }
        }

        private async ValueTask HandlePlayerCreationSuccessAsync(GameClient client)
        {
            Log.Information("New player '{PlayerName}' (ID: {PlayerId}) created for ClientId {ClientId}",
                 client.Player?.Name, client.Player?.Id, client.ClientId);

            if (client.Player == null)
            {
                Log.Error("Failed to assign created player to client {ClientId}", client.ClientId);
                return;
            }

            Log.Debug("Sending ANSWER_OK and HeroInfo to client {ClientId}", client.ClientId);
            await client.SendPacketAsync(PacketFactory.CreateTalkPacket("SYSTEM", "ALLUSERS", "", "ANSWER_OK", ChatType.Dialog, 0));
            await client.SendPacketAsync(PacketFactory.CreateHeroInfoPacket(client.Player));
            _ = await GameRuntime.GameWorld.AddPlayerAsync(client.Player, client.Player.MapId);
        }

        private async ValueTask HandlePlayerCreationFailureAsync(GameClient client)
        {
            Log.Warning("Failed to save new player for ClientId {ClientId}", client.ClientId);
        }
    }

}
