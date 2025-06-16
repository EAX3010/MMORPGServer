namespace MMORPGServer.Infrastructure.Persistence.Entities
{
    public abstract class DatabaseObject
    {
        // Audit timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Audit trail - who created/modified
        public uint? CreatedById { get; set; }  // Player/Admin ID who created this
        public uint? UpdatedById { get; set; }  // Player/Admin ID who last updated this

        // Soft delete support
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public uint? DeletedById { get; set; }  // Who deleted this

        // Optimistic concurrency control
        public uint Version { get; set; } = 1;  // Incremented on each update

        // Optional: Row-level security/ownership
        public uint? OwnerId { get; set; }  // Which player owns this data
    }
}