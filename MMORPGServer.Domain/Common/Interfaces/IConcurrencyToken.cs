namespace MMORPGServer.Domain.Common.Interfaces
{
    /// <summary>
    /// Interface for entities that support optimistic concurrency control.
    /// Prevents concurrent updates from overwriting each other's changes.
    /// </summary>
    public interface IConcurrencyToken
    {
        /// <summary>
        /// Row version for optimistic concurrency control.
        /// This value is automatically managed by SQL Server.
        /// </summary>
        byte[] RowVersion { get; set; }
    }
}