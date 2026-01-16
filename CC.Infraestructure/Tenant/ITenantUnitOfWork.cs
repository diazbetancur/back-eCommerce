using CC.Domain.Favorites;
using CC.Domain.Interfaces.Repositories;
using DomainEntities = CC.Domain.Entities;
using CC.Domain.Users;
using TenantEntities = CC.Infraestructure.Tenant.Entities;

namespace CC.Infraestructure.Tenant
{
  /// <summary>
  /// Unit of Work pattern for multi-tenant database operations.
  /// Provides transaction management and access to typed repositories.
  /// </summary>
  public interface ITenantUnitOfWork : IAsyncDisposable, IDisposable
  {
    #region Authentication & Authorization
    ITenantRepository<TenantEntities.User> Users { get; }
    ITenantRepository<TenantEntities.Role> Roles { get; }
    ITenantRepository<TenantEntities.UserRole> UserRoles { get; }
    ITenantRepository<TenantEntities.Module> Modules { get; }
    ITenantRepository<TenantEntities.RoleModulePermission> RoleModulePermissions { get; }
    #endregion

    #region User Profiles (Extended Data)
    ITenantRepository<UserProfile> UserProfiles { get; }
    #endregion

    #region Catalog
    ITenantRepository<TenantEntities.Product> Products { get; }
    ITenantRepository<TenantEntities.Category> Categories { get; }
    ITenantRepository<TenantEntities.ProductCategory> ProductCategories { get; }
    ITenantRepository<TenantEntities.ProductImage> ProductImages { get; }
    #endregion

    #region Shopping Cart
    ITenantRepository<TenantEntities.Cart> Carts { get; }
    ITenantRepository<TenantEntities.CartItem> CartItems { get; }
    #endregion

    #region Orders
    ITenantRepository<TenantEntities.Order> Orders { get; }
    ITenantRepository<TenantEntities.OrderItem> OrderItems { get; }
    ITenantRepository<TenantEntities.OrderStatus> OrderStatuses { get; }
    #endregion

    #region Favorites
    ITenantRepository<FavoriteProduct> FavoriteProducts { get; }
    #endregion

    #region Loyalty Program
    ITenantRepository<DomainEntities.LoyaltyAccount> LoyaltyAccounts { get; }
    ITenantRepository<DomainEntities.LoyaltyTransaction> LoyaltyTransactions { get; }
    #endregion

    #region Settings
    ITenantRepository<TenantEntities.TenantSetting> Settings { get; }
    ITenantRepository<TenantEntities.WebPushSubscription> WebPushSubscriptions { get; }
    #endregion

    #region Stores & Inventory
    ITenantRepository<TenantEntities.Store> Stores { get; }
    ITenantRepository<TenantEntities.ProductStoreStock> ProductStoreStock { get; }
    #endregion

    #region Database Operations

    /// <summary>
    /// Persist all tracked changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Begin a new database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    Task RollbackAsync(CancellationToken ct = default);

    /// <summary>
    /// Execute a raw SQL query (non-query)
    /// </summary>
    Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default, params object[] parameters);

    #endregion

    #region Repository Access

    /// <summary>
    /// Get a repository for any entity type dynamically
    /// </summary>
    ITenantRepository<TEntity> Repository<TEntity>() where TEntity : class;

    /// <summary>
    /// Access the underlying DbContext for advanced scenarios
    /// Use with caution - prefer using repositories
    /// </summary>
    TenantDbContext DbContext { get; }

    #endregion
  }
}
