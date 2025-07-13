using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMORPGServer.Database.Models;

public class PlayerEntityConfiguration : IEntityTypeConfiguration<PlayerData>
{
    /// <summary>
    /// Configures the PlayerEntity mapping to the database.
    /// </summary>
    /// <param name="builder">The entity type builder</param>
    public void Configure(EntityTypeBuilder<PlayerData> builder)
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


        //builder.Property(e => e.Class)
        //.HasConversion(
        //    p => p.ToString(),           // Convert enum to string for database
        //    p => Enum.Parse<ClassType>(p)) // Convert string back to enum
        //   .IsRequired();

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