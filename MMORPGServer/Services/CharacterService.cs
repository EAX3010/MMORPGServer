namespace MMORPGServer.Services
{
    public sealed class CharacterService : ICharacterService
    {
        private readonly ILogger<CharacterService> _logger;

        public CharacterService(ILogger<CharacterService> logger)
        {
            _logger = logger;
        }

        public ValueTask<IReadOnlyList<CharacterInfo>> GetCharactersByUserIdAsync(uint userId)
        {
            var characters = new List<CharacterInfo>
            {
                new(12345, "TestWarrior", 50, 1, 1002, 300, 300),
                new(12346, "TestMage", 25, 3, 1002, 250, 250)
            };

            return ValueTask.FromResult<IReadOnlyList<CharacterInfo>>(characters);
        }

        public ValueTask<CharacterInfo?> GetCharacterAsync(uint characterId)
        {
            if (characterId == 12345)
            {
                return ValueTask.FromResult<CharacterInfo?>(
                    new CharacterInfo(12345, "TestWarrior", 50, 1, 1002, 300, 300));
            }

            return ValueTask.FromResult<CharacterInfo?>(null);
        }

        public ValueTask<uint> CreateCharacterAsync(uint userId, string name, ushort characterClass)
        {
            _logger.LogInformation("Creating character {Name} for user {UserId}", name, userId);
            return ValueTask.FromResult(12347u);
        }

        public ValueTask<bool> DeleteCharacterAsync(uint characterId)
        {
            _logger.LogInformation("Deleting character {CharacterId}", characterId);
            return ValueTask.FromResult(true);
        }
    }
}