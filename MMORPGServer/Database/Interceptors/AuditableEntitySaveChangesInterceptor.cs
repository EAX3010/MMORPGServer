using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MMORPGServer.Database.Common.Interfaces;

namespace MMORPGServer.Database.Interceptors
{
    /// <summary>
    /// Interceptor that automatically manages audit timestamps for entities.
    /// Handles creation time, modification time, and soft delete operations.
    /// </summary>
    public sealed class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
    {
        /// <summary>
        /// Intercepts synchronous SaveChanges calls to update audit fields.
        /// </summary>
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        /// <summary>
        /// Intercepts asynchronous SaveChangesAsync calls to update audit fields.
        /// </summary>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        /// <summary>
        /// Updates audit fields for all tracked entities based on their state.
        /// </summary>
        /// <param name="context">The database context</param>
        private void UpdateEntities(DbContext? context)
        {
            if (context == null) return;

            // Get current UTC time for consistency
            var currentTime = DateTime.UtcNow;

            // Process all entities inheriting from BaseEntity
            foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = currentTime;
                        entry.Entity.LastModifiedAt = currentTime;
                        break;

                    case EntityState.Modified:
                        entry.Entity.LastModifiedAt = currentTime;
                        entry.Property(e => e.CreatedAt).IsModified = false;
                        break;

                    case EntityState.Deleted:
                        if (entry.Entity is ISoftDeletable softDeletable)
                        {
                            // Convert hard delete to soft delete
                            entry.State = EntityState.Modified;
                            softDeletable.IsDeleted = true;
                            softDeletable.DeletedAt = currentTime;
                            entry.Entity.LastModifiedAt = currentTime;
                        }
                        break;
                }
            }
        }
    }
}