namespace MMORPGServer.Domain.Common
{
    /// <summary>
    /// Interface for entities that support soft delete functionality.
    /// Soft deleted entities are marked as deleted but not physically removed from the database.
    /// </summary>
    public interface ISoftDeletable
    {
        /// <summary>
        /// Indicates whether the entity is soft deleted.
        /// </summary>
        bool IsDeleted { get; set; }

        /// <summary>
        /// The timestamp when the entity was soft deleted.
        /// </summary>
        DateTime? DeletedAt { get; set; }
    }
}
