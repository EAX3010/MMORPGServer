using Microsoft.EntityFrameworkCore;
using MMORPGServer.Database.Common.Interfaces;
using MMORPGServer.Database.Models;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Entity Framework Core database context for the MMORPG game.
/// Manages database connections and entity configurations.
/// </summary>
public sealed class GameDbContext : DbContext
{

    /// <summary>
    /// Initializes a new instance of the GameDbContext.
    /// </summary>
    /// <param name="options">Database context options</param>
    public GameDbContext(
        DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet for Player entities.
    /// </summary>
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();

    /// <summary>
    /// Configures the model creating conventions and relationships.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Apply global query filters (e.g., soft delete)
        ApplyGlobalFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters to all entities.
    /// Currently applies soft delete filter to exclude deleted entities.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        // Apply soft delete filter to all entities implementing ISoftDeletable
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                // Create and apply the filter expression: e => !e.IsDeleted
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(CreateSoftDeleteFilter(entityType.ClrType));
            }
        }
    }

    /// <summary>
    /// Creates a lambda expression for filtering soft deleted entities.
    /// </summary>
    /// <param name="entityType">The entity type to create the filter for</param>
    /// <returns>Lambda expression that filters out soft deleted entities</returns>
    private static LambdaExpression CreateSoftDeleteFilter(Type entityType)
    {
        // Create parameter: e
        var parameter = Expression.Parameter(entityType, "e");

        // Create property access: e.IsDeleted
        var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));

        // Create constant: false
        var constant = Expression.Constant(false);

        // Create equality check: e.IsDeleted == false
        var body = Expression.Equal(property, constant);

        // Create lambda: e => e.IsDeleted == false
        return Expression.Lambda(body, parameter);
    }
}