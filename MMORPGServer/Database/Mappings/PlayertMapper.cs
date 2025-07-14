using MMORPGServer.Common.ValueObjects;
using MMORPGServer.Database.Models;
using MMORPGServer.Entities;

namespace MMORPGServer.Database.Mappings
{
    /// <summary>
    /// Maps between Player (runtime) and PlayerEntity (database) models.
    /// </summary>
    public static class PlayerMapper
    {
        /// <summary>
        /// Creates a new runtime Player from a database PlayerEntity.
        /// Used when player logs in.
        /// </summary>
        public static Player ToGameObject(this PlayerData entity)
        {
            var player = new Player(entity.Id)
            {
                Name = entity.Name,
                Level = entity.Level,
                Experience = entity.Experience,
                Gold = entity.Gold,
                ConquerPoints = entity.ConquerPoints,
                BoundConquerPoints = entity.BoundConquerPoints,
                Face = entity.Face,
                Body = entity.Body,
                Hair = entity.Hair,
                Class = entity.Class,
                ClassLevel = entity.ClassLevel,
                CreatedAtMacAddress = entity.CreatedAtMacAddress,
                CreatedFingerPrint = entity.CreatedFingerPrint,
                Position = new Position(entity.X, entity.Y),
                MapId = entity.MapId,
                MaxHealth = entity.MaxHealth,
                CurrentHealth = entity.CurrentHealth,
                MaxMana = entity.MaxMana,
                CurrentMana = entity.CurrentMana,
                Strength = entity.Strength,
                Agility = entity.Agility,
                Vitality = entity.Vitality,
                Spirit = entity.Spirit,

                LastLogin = DateTime.UtcNow,
            };

            return player;
        }

        /// <summary>
        /// Creates a new PlayerEntity for a brand new player.
        /// Used during character creation.
        /// </summary>
        public static PlayerData ToDatabaseObject(this Player entity)
        {
            return new PlayerData
            {
                Id = entity.Id,
                Name = entity.Name,
                Level = entity.Level,
                Experience = entity.Experience,
                Gold = entity.Gold,
                ConquerPoints = entity.ConquerPoints,
                BoundConquerPoints = entity.BoundConquerPoints,
                Face = entity.Face,
                Body = entity.Body,
                Hair = entity.Hair,
                Class = entity.Class,
                ClassLevel = entity.ClassLevel,
                CreatedAtMacAddress = entity.CreatedAtMacAddress,
                CreatedFingerPrint = entity.CreatedFingerPrint,
                X = entity.Position.X,
                Y = entity.Position.Y,
                MapId = entity.MapId,
                MaxHealth = entity.MaxHealth,
                CurrentHealth = entity.CurrentHealth,
                MaxMana = entity.MaxMana,
                CurrentMana = entity.CurrentMana,
                Strength = entity.Strength,
                Agility = entity.Agility,
                Vitality = entity.Vitality,
                Spirit = entity.Spirit,
                LastLogin = entity.LastLogin,

                LastLogout = DateTime.UtcNow
            };
        }
    }
}