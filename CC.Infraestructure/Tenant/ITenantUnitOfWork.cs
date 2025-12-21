using CC.Domain.Favorites;
using CC.Domain.Interfaces.Repositories;
using CC.Domain.Loyalty;
using CC.Domain.Users;
using CC.Infraestructure.Tenant.Entities;

namespace CC.Infraestructure.Tenant
{
  /// <summary>
  /// Unit of Work pattern for multi-tenant database operations.
  /// Provides transaction management and access to typed repositories.
  /// </summary>
  public interface ITenantUnitOfWork : IAsyncDisposable, IDisposable
  {
    #region Authentication & Authorization
    ITenantRepository<TenantUser> Users { get; }
    ITenantRepository<TenantRole> Roles { get; }
    ITenantRepository<TenantUserRole> UserRoles { get; }
    ITenantRepository<Module> Modules { get; }
    ITenantRepository<RoleModulePermission> RoleModulePermissions { get; }
    #endregion

    #region User Accounts (Consumer)
    ITenantRepository<UserAccount> UserAccounts { get; }
    ITenantRepository<UserProfile> UserProfiles { get; }
    #endregion

    #region Catalog
    ITenantRepository<Product> Products { get; }
    ITenantRepository<Category> Categories { get; }
    ITenantRepository<ProductCategory> ProductCategories { get; }
    ITenantRepository<ProductImage> ProductImages { get; }
    #endregion

    #region Shopping Cart
    ITenantRepository<Cart> Carts { get; }
    ITenantRepository<CartItem> CartItems { get; }
    #endregion

    #region Orders
    ITenantRepository<Order> Orders { get; }
    ITenantRepository<OrderItem> OrderItems { get; }
    ITenantRepository<OrderStatus> OrderStatuses { get; }
    #endregion

    #region Favorites
    ITenantRepository<FavoriteProduct> FavoriteProducts { get; }
    #endregion

    #region Loyalty Program
    ITenantRepository<LoyaltyAccount> LoyaltyAccounts { get; }
    ITenantRepository<LoyaltyTransaction> LoyaltyTransactions { get; }
    #endregion

    #region Settings
    ITenantRepository<TenantSetting> Settings { get; }
    ITenantRepository<WebPushSubscription> WebPushSubscriptions { get; }
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
