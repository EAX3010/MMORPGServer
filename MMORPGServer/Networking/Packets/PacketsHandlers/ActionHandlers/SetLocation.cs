using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Structures;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers.ActionHandlers
{
    [ActionHandler(ActionType.SetLocation)]
    internal sealed class SetLocation : ActionHandlerBase, IActionHandler
    {
        public async ValueTask<bool> HandleAsync(GameClient client, ActionProto action)
        {
            try
            {
                // Validate player state before processing
                if (client.Player.Position == null)
                {
                    Log.Warning("Player {PlayerId} has no position data for SetLocation action", client.Player.Id);
                    return false;
                }

                Log.Debug("Handling SetLocation for player {PlayerId}", client.Player.Id);

                // Create response packet

                // Note: You'll need to pass the packet builder or create a new one
                // This assumes you have access to a packet builder service
                var packetData = Build(new ActionProto
                {
                    UID = client.Player.Id,
                    Type = ActionType.SetLocation,
                    dwParam = client.Player.MapId,
                    wParam1 = client.Player.Position.X,
                    wParam2 = client.Player.Position.Y,
                });
                await client.SendPacketAsync(packetData);

                Log.Debug("SetLocation response sent to player {PlayerId} - Map: {MapId}, Position: ({X}, {Y})",
                    client.Player.Id, client.Player.MapId, client.Player.Position.X, client.Player.Position.Y);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling SetLocation action for player {PlayerId}", client.Player.Id);
                return false;
            }
        }


    }
}