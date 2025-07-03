using MMORPGServer.Common.Enums;
using MMORPGServer.Entities;
using MMORPGServer.Networking.Packets.PacketsProto;

namespace MMORPGServer.Common.Interfaces
{
    public interface IPacketFactory
    {
        ReadOnlyMemory<byte> CreateActionPacket(ActionProto proto);
        ReadOnlyMemory<byte> CreateHeroInfoPacket(Player player);
        ReadOnlyMemory<byte> CreateLoginGamaEnglish();
        ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, int mesh);
    }
}