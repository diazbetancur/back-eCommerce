using System.Collections.Concurrent;
using CC.Domain.Favorites;
using CC.Domain.Interfaces.Repositories;
using CC.Domain.Loyalty;
using CC.Domain.Users;
using CC.Infraestructure.Tenant.Entities;
using CC.Infraestructure.Tenant.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.Tenant
{
  /// <summary>
  /// Implementation of Unit of Work pattern for multi-tenant database operations.
  /// Manages DbContext lifecycle, repositories, and transactions.
  /// </summary>
  public sealed class TenantUnitOfWork : ITenantUnitOfWork
  {
    private readonly TenantDbContext _context;
    private readonly ILogger<TenantUnitOfWork> _logger;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    #region Lazy-loaded Repositories
    private ITenantRepository<TenantUser>? _users;
    private ITenantRepository<TenantRole>? _roles;
    private ITenantRepository<TenantUserRole>? _userRoles;
    private ITenantRepository<Module>? _modules;
    private ITenantRepository<RoleModulePermission>? _roleModulePermissions;
    private ITenantRepository<UserAccount>? _userAccounts;
    private ITenantRepository<UserProfile>? _userProfiles;
    private ITenantRepository<Product>? _products;
    private ITenantRepository<Category>? _categories;
    private ITenantRepository<ProductCategory>? _productCategories;
    private ITenantRepository<ProductImage>? _productImages;
    private ITenantRepository<Cart>? _carts;
    private ITenantRepository<CartItem>? _cartItems;
    private ITenantRepository<Order>? _orders;
    private ITenantRepository<OrderItem>? _orderItems;
    private ITenantRepository<OrderStatus>? _orderStatuses;
    private ITenantRepository<FavoriteProduct>? _favoriteProducts;
    private ITenantRepository<LoyaltyAccount>? _loyaltyAccounts;
    private ITenantRepository<LoyaltyTransaction>? _loyaltyTransactions;
    private ITenantRepository<TenantSetting>? _settings;
    private ITenantRepository<WebPushSubscription>? _webPushSubscriptions;
    #endregion

    public TenantUnitOfWork(TenantDbContext context, ILogger<TenantUnitOfWork> logger)
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Authentication & Authorization
    public ITenantRepository<TenantUser> Users =>
        _users ??= new TenantRepository<TenantUser>(_context);

    public ITenantRepository<TenantRole> Roles =>
        _roles ??= new TenantRepository<TenantRole>(_context);

    public ITenantRepository<TenantUserRole> UserRoles =>
        _userRoles ??= new TenantRepository<TenantUserRole>(_context);

    public ITenantRepository<Module> Modules =>
        _modules ??= new TenantRepository<Module>(_context);

    public ITenantRepository<RoleModulePermission> RoleModulePermissions =>
        _roleModulePermissions ??= new TenantRepository<RoleModulePermission>(_context);
    #endregion

    #region User Accounts (Consumer)
    public ITenantRepository<UserAccount> UserAccounts =>
        _userAccounts ??= new TenantRepository<UserAccount>(_context);

    public ITenantRepository<UserProfile> UserProfiles =>
        _userProfiles ??= new TenantRepository<UserProfile>(_context);
    #endregion

    #region Catalog
    public ITenantRepository<Product> Products =>
        _products ??= new TenantRepository<Product>(_context);

    public ITenantRepository<Category> Categories =>
        _categories ??= new TenantRepository<Category>(_context);

    public ITenantRepository<ProductCategory> ProductCategories =>
        _productCategories ??= new TenantRepository<ProductCategory>(_context);

    public ITenantRepository<ProductImage> ProductImages =>
        _productImages ??= new TenantRepository<ProductImage>(_context);
    #endregion

    #region Shopping Cart
    public ITenantRepository<Cart> Carts =>
        _carts ??= new TenantRepository<Cart>(_context);

    public ITenantRepository<CartItem> CartItems =>
        _cartItems ??= new TenantRepository<CartItem>(_context);
    #endregion

    #region Orders
    public ITenantRepository<Order> Orders =>
        _orders ??= new TenantRepository<Order>(_context);

    public ITenantRepository<OrderItem> OrderItems =>
        _orderItems ??= new TenantRepository<OrderItem>(_context);

    public ITenantRepository<OrderStatus> OrderStatuses =>
        _orderStatuses ??= new TenantRepository<OrderStatus>(_context);
    #endregion

    #region Favorites
    public ITenantRepository<FavoriteProduct> FavoriteProducts =>
        _favoriteProducts ??= new TenantRepository<FavoriteProduct>(_context);
    #endregion

    #region Loyalty Program
    public ITenantRepository<LoyaltyAccount> LoyaltyAccounts =>
        _loyaltyAccounts ??= new TenantRepository<LoyaltyAccount>(_context);

    public ITenantRepository<LoyaltyTransaction> LoyaltyTransactions =>
        _loyaltyTransactions ??= new TenantRepository<LoyaltyTransaction>(_context);
    #endregion

    #region Settings
    public ITenantRepository<TenantSetting> Settings =>
        _settings ??= new TenantRepository<TenantSetting>(_context);

    public ITenantRepository<WebPushSubscription> WebPushSubscriptions =>
        _webPushSubscriptions ??= new TenantRepository<WebPushSubscription>(_context);
    #endregion

    #region DbContext Access
    public TenantDbContext DbContext => _context;
    #endregion

    #region Generic Repository Access
    public ITenantRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
      var entityType = typeof(TEntity);

      return (ITenantRepository<TEntity>)_repositories.GetOrAdd(
          entityType,
          _ => new TenantRepository<TEntity>(_context));
    }
    #endregion

    #region Database Operations
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
      try
      {
        return await _context.SaveChangesAsync(ct);
      }
      catch (DbUpdateConcurrencyException ex)
      {
        _logger.LogError(ex, "Concurrency conflict while saving changes");
        throw;
      }
      catch (DbUpdateException ex)
      {
        _logger.LogError(ex, "Error saving changes to database");
        throw;
      }
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
      if (_transaction != null)
      {
        throw new InvalidOperationException("A transaction is already in progress");
      }

      _transaction = await _context.Database.BeginTransactionAsync(ct);
      _logger.LogDebug("Transaction started");
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
      if (_transaction == null)
      {
        throw new InvalidOperationException("No transaction in progress to commit");
      }

      try
      {
        await _context.SaveChangesAsync(ct);
        await _transaction.CommitAsync(ct);
        _logger.LogDebug("Transaction committed");
      }
      catch (Exception)
      {
        await RollbackAsync(ct);
        throw;
      }
      finally
      {
        await _transaction.DisposeAsync();
        _transaction = null;
      }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
      if (_transaction == null)
      {
        _logger.LogWarning("No transaction in progress to rollback");
        return;
      }

      try
      {
        await _transaction.RollbackAsync(ct);
        _logger.LogDebug("Transaction rolled back");
      }
      finally
      {
        await _transaction.DisposeAsync();
        _transaction = null;
      }
    }

    public async Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default, params object[] parameters)
    {
      return await _context.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }
    #endregion

    #region Dispose
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
      await DisposeAsyncCore();
      Dispose(false);
      GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
      if (_disposed)
        return;

      if (disposing)
      {
        _transaction?.Dispose();
        _context.Dispose();
        _repositories.Clear();
      }

      _disposed = true;
    }

    private async ValueTask DisposeAsyncCore()
    {
      if (_transaction != null)
      {
        await _transaction.DisposeAsync();
        _transaction = null;
      }

      await _context.DisposeAsync();
      _repositories.Clear();
    }
    #endregion
  }
}
