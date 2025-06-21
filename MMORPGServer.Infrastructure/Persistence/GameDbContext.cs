using Microsoft.EntityFrameworkCore;
using MMORPGServer.Application.Common.Interfaces;
using MMORPGServer.Domain.Common;
using MMORPGServer.Domain.Persistence;
using MMORPGServer.Infrastructure.Persistence.Interceptors;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Entity Framework Core database context for the MMORPG game.
/// Manages database connections and entity configurations.
/// </summary>
public sealed class GameDbContext : DbContext, IApplicationDbContext
{
    private readonly AuditableEntitySaveChangesInterceptor _auditableEntitySaveChangesInterceptor;

    /// <summary>
    /// Initializes a new instance of the GameDbContext.
    /// </summary>
    /// <param name="options">Database context options</param>
    /// <param name="auditableEntitySaveChangesInterceptor">Interceptor for handling audit fields</param>
    public GameDbContext(
        DbContextOptions<GameDbContext> options,
        AuditableEntitySaveChangesInterceptor auditableEntitySaveChangesInterceptor)
        : base(options)
    {
        _auditableEntitySaveChangesInterceptor = auditableEntitySaveChangesInterceptor;
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
    /// Configures database-specific options.
    /// </summary>
    /// <param name="optionsBuilder">The options builder instance</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Add the audit interceptor to automatically handle timestamps
        optionsBuilder.AddInterceptors(_auditableEntitySaveChangesInterceptor);

#if DEBUG
        // Enable detailed logging in debug mode for troubleshooting
        // optionsBuilder.EnableSensitiveDataLogging();
        // optionsBuilder.EnableDetailedErrors();
#endif
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