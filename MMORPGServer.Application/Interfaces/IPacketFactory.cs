using MMORPGServer.Domain.Entities;
using MMORPGServer.Domain.Enums;
using MMORPGServer.Domain.PacketsProto;

namespace MMORPGServer.Application.Interfaces
{
    public interface IPacketFactory
    {
        ReadOnlyMemory<byte> CreateActionPacket(ActionProto proto);
        ReadOnlyMemory<byte> CreateHeroInfoPacket(Player player);
        ReadOnlyMemory<byte> CreateLoginGamaEnglish();
        ReadOnlyMemory<byte> CreateTalkPacket(string from, string to, string suffix, string message, ChatType chatType, uint mesh);
    }
}