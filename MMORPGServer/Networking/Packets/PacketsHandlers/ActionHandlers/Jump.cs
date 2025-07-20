using MMORPGServer.Common.Enums;
using MMORPGServer.Common.Interfaces;
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

                Log.Debug("Handling SetLocation for player {PlayerId}", client.Player.Id);

                Common.ValueObjects.Position newPosition = new Common.ValueObjects.Position(action.dwParam_Lo, (short)action.dwParam_Hi);
                if (client.Player.Position.InRange(newPosition, 18))
                {
                    if (await client.Player.Map.MovePlayerAsync(client.Player, newPosition))
                    {
                        ActionProto responseAction = new ActionProto
                        {
                            UID = client.Player.Id,
                            Type = ActionType.Jump,
                            dwParam_Lo = client.Player.Position.X,
                            dwParam_Hi = client.Player.Position.Y,
                        };

                        ReadOnlyMemory<byte> packetData = Build(responseAction);
                        await client.SendPacketAsync(packetData);

                        Log.Debug("Jump response sent to player {PlayerId} - Map: {MapId}, Position: ({X}, {Y})",
                            client.Player.Id, client.Player.MapId, client.Player.Position.X, client.Player.Position.Y);
                        return true;
                    }
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