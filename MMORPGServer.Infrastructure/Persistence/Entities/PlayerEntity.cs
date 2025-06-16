namespace MMORPGServer.Infrastructure.Persistence.Entities
{
    public class PlayerEntity : DatabaseObject
    {
        // Primary Key
        public uint Id { get; set; }

        // Basic Info
        public string Name { get; set; }
        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;

        // Position
        public short PositionX { get; set; }
        public short PositionY { get; set; }
        public ushort MapId { get; set; } = 1001;

        // Resources
        public long Gold { get; set; } = 0;

        // Player-specific timestamps
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogout { get; set; }

        // Inherited from DatabaseObject:
        // CreatedAt, UpdatedAt, CreatedById, UpdatedById
        // IsDeleted, DeletedAt, DeletedById, Version, OwnerId
    }
}