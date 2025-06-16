using Microsoft.EntityFrameworkCore;
using MMORPGServer.Infrastructure.Persistence.Entities;

namespace MMORPGServer.Infrastructure.Persistence
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<PlayerEntity> Players { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure base DatabaseObject properties for all entities
            ConfigureDatabaseObject(modelBuilder);

            // Configure specific entities
            ConfigurePlayerEntity(modelBuilder);
        }

        private void ConfigureDatabaseObject(ModelBuilder modelBuilder)
        {
            // Apply to all entities that inherit from DatabaseObject
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(DatabaseObject).IsAssignableFrom(entityType.ClrType))
                {
                    // Configure common properties
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(DatabaseObject.CreatedAt))
                        .HasDefaultValueSql("NOW()");

                    modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(DatabaseObject.UpdatedAt))
                        .HasDefaultValueSql("NOW()");

                    modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(DatabaseObject.Version))
                        .HasDefaultValue(1)
                        .IsConcurrencyToken(); // Enable optimistic concurrency

                    modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(DatabaseObject.IsDeleted))
                        .HasDefaultValue(false);

                    // Add index on IsDeleted for performance
                    modelBuilder.Entity(entityType.ClrType)
                        .HasIndex(nameof(DatabaseObject.IsDeleted));
                }
            }
        }

        private void ConfigurePlayerEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlayerEntity>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Name)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.HasIndex(e => e.Name)
                    .IsUnique();

                entity.Property(e => e.Level)
                    .HasDefaultValue(1);

                entity.Property(e => e.Experience)
                    .HasDefaultValue(0);

                entity.Property(e => e.Gold)
                    .HasDefaultValue(0);

                entity.Property(e => e.MapId)
                    .HasDefaultValue(1001);

                // Indexes for common queries
                entity.HasIndex(e => e.MapId);
                entity.HasIndex(e => e.Level);
                entity.HasIndex(e => e.LastLogin);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Enable sensitive data logging in development
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
#endif
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Automatically update UpdatedAt timestamp
            foreach (var entry in ChangeTracker.Entries<DatabaseObject>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        entry.Entity.Version = 1;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                        entry.Entity.Version++;
                        break;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}