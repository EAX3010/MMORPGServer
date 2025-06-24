using MMORPGServer.Domain.Common.Enums;
using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.PacketsProto;

namespace MMORPGServer.Domain.Common.Interfaces
{
    public interface IPacketFactory
    {
        ReadOnlyMemory<byte> CreateActionPacket(ActionProto proto);
        ReadOnlyMemory<byte> CreateHeroInfoPacket(Player player);
        ReadOnlyMemory<byte> CreateLoginGamaEnglish();
        ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, int mesh);
    }
}