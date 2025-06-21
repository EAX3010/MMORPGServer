using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MMORPGServer.Domain.Persistence;

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
            .HasMaxLength(15)
            .UseCollation("LATIN1_GENERAL_100_CI_AS_SC_UTF8"); // Case-insensitive collation supporting Arabic

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
        builder.Property(e => e.LastLogin);

        // Last logout: Optional (null when player is online)
        builder.Property(e => e.LastLogout);

        // === Audit Fields ===

        // Created timestamp: Set by default to current UTC time
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // Modified timestamp: Optional (null until first modification)
        builder.Property(e => e.LastModifiedAt).IsRequired()
             .HasDefaultValueSql("GETUTCDATE()");

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
            .HasDatabaseName("IX_Players_Name");

        // Index on last login for activity queries
        builder.HasIndex(e => e.LastLogin)
            .HasDatabaseName("IX_Players_LastLogin");

        // Index on soft delete flag
        builder.HasIndex(e => e.IsDeleted)
            .HasDatabaseName("IX_Players_IsDeleted");

    }
}