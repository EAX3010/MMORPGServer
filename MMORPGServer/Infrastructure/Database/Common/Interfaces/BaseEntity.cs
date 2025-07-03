namespace MMORPGServer.Infrastructure.Database.Common.Interfaces
{
    /// <summary>
    /// Base entity class that provides common properties for all domain entities.
    /// Implements soft delete pattern and optimistic concurrency control.
    /// </summary>
    public abstract class BaseEntity : ISoftDeletable, IConcurrencyToken
    {
        /// <summary>
        /// The date and time when the entity was created.
        /// Automatically set by the system when the entity is first saved.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The date and time when the entity was last modified.
        /// Null if the entity has never been modified after creation.
        /// </summary>
        public DateTime? LastModifiedAt { get; set; }

        /// <summary>
        /// Indicates whether the entity has been soft deleted.
        /// Soft deleted entities are excluded from normal queries but remain in the database.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// The date and time when the entity was soft deleted.
        /// Null if the entity has not been deleted.
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Concurrency token used for optimistic concurrency control.
        /// SQL Server automatically updates this value on each save.
        /// </summary>
        public byte[] RowVersion { get; set; } = default!;
    }
}