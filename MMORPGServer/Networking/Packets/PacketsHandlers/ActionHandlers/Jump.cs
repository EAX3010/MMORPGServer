using MMORPGServer.Common.Enums;
using MMORPGServer.Networking.Clients;
using MMORPGServer.Networking.Packets.Attributes;
using MMORPGServer.Networking.Packets.Structures;
using Serilog;

namespace MMORPGServer.Networking.Packets.PacketsHandlers.ActionHandlers
{
    [ActionHandler(ActionType.Jump)]
    internal sealed class Jump : ActionHandlerBase, IActionHandler
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

                var Newposition = new Common.ValueObjects.Position((short)action.dwParam_Lo, (short)action.dwParam_Hi);
                if (await client.Player.Map.MovePlayerAsync(client.Player.Id, Newposition))
                {
                    // Create response packet
                    client.Player.Position = Newposition;
                    var responseAction = new ActionProto
                    {
                        UID = client.Player.Id,
                        Type = ActionType.Jump,
                        dwParam_Lo = client.Player.Position.X,
                        dwParam_Hi = client.Player.Position.Y,
                    };

                    var packetData = Build(responseAction);
                    await client.SendPacketAsync(packetData);

                    Log.Debug("Jump response sent to player {PlayerId} - Map: {MapId}, Position: ({X}, {Y})",
                        client.Player.Id, client.Player.MapId, client.Player.Position.X, client.Player.Position.Y);
                    return true;
                }
                return false;

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling SetLocation action for player {PlayerId}", client.Player.Id);
                return false;
            }
        }


    }
}