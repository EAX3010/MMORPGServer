namespace MMORPGServer.Interfaces
{
    public interface ICharacterService
    {
        ValueTask<IReadOnlyList<CharacterInfo>> GetCharactersByUserIdAsync(uint userId);
        ValueTask<CharacterInfo?> GetCharacterAsync(uint characterId);
        ValueTask<uint> CreateCharacterAsync(uint userId, string name, ushort characterClass);
        ValueTask<bool> DeleteCharacterAsync(uint characterId);
    }
    public record CharacterInfo(
        uint CharacterId,
        string Name,
        ushort Level,
        ushort Class,
        uint MapId,
        ushort X,
        ushort Y
    );
}