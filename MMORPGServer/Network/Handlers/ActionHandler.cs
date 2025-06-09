using MMORPGServer.Network.Packets;
using ProtoBuf;

namespace MMORPGServer.Network.Handlers
{
    public class ActionHandler : IPacketProcessor
    {
        [PacketHandler(GamePackets.CMsgAction)]
        public ValueTask HandleAsync(IGameClient client, Packet packet)
        {
            using (var ms = new System.IO.MemoryStream(packet.Data.Slice(4, packet.Data.Length - 12).ToArray()))
            {
                var Action = ProtoBuf.Serializer.Deserialize<ActionProto>(ms);
                switch (Action.Type)
                {
                    case 74://SetLoctionOnTheMap for spawn
                        {
                            var ResponsePacket = new ActionProto
                            {
                                dwParam = 1002,
                                wParam1 = (ushort)client.Player.Position.X,
                                wParam2 = (ushort)client.Player.Position.Y,
                                Type = Action.Type,
                                UID = Action.UID

                            };

                            // Serialize our object to a byte array using Protobuf
                            using var memoryStream = new MemoryStream();
                            Serializer.Serialize(memoryStream, ResponsePacket);
                            var payload = memoryStream.ToArray();

                            // Use the new PacketBuilder to construct the final packet for sending
                            client.SendPacketAsync(PacketFactory.CreateProtoPacket(GamePackets.CMsgAction, payload));
                            break;

                        }
                }
            }
            return ValueTask.CompletedTask;
        }

    }
}
