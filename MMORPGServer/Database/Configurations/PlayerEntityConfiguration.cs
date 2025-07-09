using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMORPGServer.Database.Models;

public class PlayerEntityConfiguration : IEntityTypeConfiguration<PlayerEntity>
{
    /// <summary>
    /// Configures the PlayerEntity mapping to the database.
    /// </summary>
    /// <param name="builder">The entity type builder</param>
    public void Configure(EntityTypeBuilder<PlayerEntity> builder)
    {
        // === Table Configuration ===
        builder.ToTable("Players");

        // === Primary Key ===
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedNever();

        // === Player Properties ===

        // Name: Required, max 50 chars, case-insensitive, supports Arabic and English
        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(15); // Case-insensitive collation supporting Arabic

        // Level: Default value 1
        builder.Property(e => e.Level)
            .IsRequired()
            .HasDefaultValue(1);

        // Experience: Default value 0
        builder.Property(e => e.Experience)
            .IsRequired()
            .HasDefaultValue(0L);

        // Money: Stored as Currency column, default 0
        builder.Property(e => e.Gold)
            .IsRequired()
            .HasDefaultValue(0L)
            .HasColumnName("Gold");

        builder.Property(e => e.MapId)
       .IsRequired();
        builder.Property(e => e.X)
         .IsRequired();
        builder.Property(e => e.Y)
             .IsRequired();


        // Last login: Required with default current UTC time
        _ = builder.Property(e => e.LastLogin);

        // Last logout: Optional (null when player is online)
        _ = builder.Property(e => e.LastLogout);

        // === Audit Fields ===

        // Created timestamp: Set by default to current UTC time
        _ = builder.Property(e => e.CreatedAt);


        // Modified timestamp: Optional (null until first modification)
        _ = builder.Property(e => e.LastModifiedAt);

        // === Soft Delete Fields ===

        // IsDeleted flag: Default false
        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // Deletion timestamp: Optional
        builder.Property(e => e.DeletedAt);

        // === Concurrency Control ===

        // Row version for optimistic concurrency
        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // === Indexes for Query Performance ===

        // Unique index on player name
        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("idx_players_name");

        // Index on soft delete flag
        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName("idx_players_is_deleted");

    }
}