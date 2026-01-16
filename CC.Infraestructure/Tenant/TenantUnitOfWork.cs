using System.Collections.Concurrent;
using CC.Domain.Favorites;
using CC.Domain.Interfaces.Repositories;
using DomainEntities = CC.Domain.Entities;
using CC.Domain.Users;
using TenantEntities = CC.Infraestructure.Tenant.Entities;
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
    private ITenantRepository<TenantEntities.User>? _users;
    private ITenantRepository<TenantEntities.Role>? _roles;
    private ITenantRepository<TenantEntities.UserRole>? _userRoles;
    private ITenantRepository<TenantEntities.Module>? _modules;
    private ITenantRepository<TenantEntities.RoleModulePermission>? _roleModulePermissions;
    private ITenantRepository<UserProfile>? _userProfiles;
    private ITenantRepository<TenantEntities.Product>? _products;
    private ITenantRepository<TenantEntities.Category>? _categories;
    private ITenantRepository<TenantEntities.ProductCategory>? _productCategories;
    private ITenantRepository<TenantEntities.ProductImage>? _productImages;
    private ITenantRepository<TenantEntities.Cart>? _carts;
    private ITenantRepository<TenantEntities.CartItem>? _cartItems;
    private ITenantRepository<TenantEntities.Order>? _orders;
    private ITenantRepository<TenantEntities.OrderItem>? _orderItems;
    private ITenantRepository<TenantEntities.OrderStatus>? _orderStatuses;
    private ITenantRepository<FavoriteProduct>? _favoriteProducts;
    private ITenantRepository<DomainEntities.LoyaltyAccount>? _loyaltyAccounts;
    private ITenantRepository<DomainEntities.LoyaltyTransaction>? _loyaltyTransactions;
    private ITenantRepository<TenantEntities.TenantSetting>? _settings;
    private ITenantRepository<TenantEntities.WebPushSubscription>? _webPushSubscriptions;
    private ITenantRepository<TenantEntities.Store>? _stores;
    private ITenantRepository<TenantEntities.ProductStoreStock>? _productStoreStock;
    #endregion

    public TenantUnitOfWork(TenantDbContext context, ILogger<TenantUnitOfWork> logger)
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Authentication & Authorization
    public ITenantRepository<TenantEntities.User> Users =>
        _users ??= new TenantRepository<TenantEntities.User>(_context);

    public ITenantRepository<TenantEntities.Role> Roles =>
        _roles ??= new TenantRepository<TenantEntities.Role>(_context);

    public ITenantRepository<TenantEntities.UserRole> UserRoles =>
        _userRoles ??= new TenantRepository<TenantEntities.UserRole>(_context);

    public ITenantRepository<TenantEntities.Module> Modules =>
        _modules ??= new TenantRepository<TenantEntities.Module>(_context);

    public ITenantRepository<TenantEntities.RoleModulePermission> RoleModulePermissions =>
        _roleModulePermissions ??= new TenantRepository<TenantEntities.RoleModulePermission>(_context);
    #endregion

    #region User Profiles (Extended Data)
    public ITenantRepository<UserProfile> UserProfiles =>
        _userProfiles ??= new TenantRepository<UserProfile>(_context);
    #endregion

    #region Catalog
    public ITenantRepository<TenantEntities.Product> Products =>
        _products ??= new TenantRepository<TenantEntities.Product>(_context);

    public ITenantRepository<TenantEntities.Category> Categories =>
        _categories ??= new TenantRepository<TenantEntities.Category>(_context);

    public ITenantRepository<TenantEntities.ProductCategory> ProductCategories =>
        _productCategories ??= new TenantRepository<TenantEntities.ProductCategory>(_context);

    public ITenantRepository<TenantEntities.ProductImage> ProductImages =>
        _productImages ??= new TenantRepository<TenantEntities.ProductImage>(_context);
    #endregion

    #region Shopping Cart
    public ITenantRepository<TenantEntities.Cart> Carts =>
        _carts ??= new TenantRepository<TenantEntities.Cart>(_context);

    public ITenantRepository<TenantEntities.CartItem> CartItems =>
        _cartItems ??= new TenantRepository<TenantEntities.CartItem>(_context);
    #endregion

    #region Orders
    public ITenantRepository<TenantEntities.Order> Orders =>
        _orders ??= new TenantRepository<TenantEntities.Order>(_context);

    public ITenantRepository<TenantEntities.OrderItem> OrderItems =>
        _orderItems ??= new TenantRepository<TenantEntities.OrderItem>(_context);

    public ITenantRepository<TenantEntities.OrderStatus> OrderStatuses =>
        _orderStatuses ??= new TenantRepository<TenantEntities.OrderStatus>(_context);
    #endregion

    #region Favorites
    public ITenantRepository<FavoriteProduct> FavoriteProducts =>
        _favoriteProducts ??= new TenantRepository<FavoriteProduct>(_context);
    #endregion

    #region Loyalty Program
    public ITenantRepository<DomainEntities.LoyaltyAccount> LoyaltyAccounts =>
        _loyaltyAccounts ??= new TenantRepository<DomainEntities.LoyaltyAccount>(_context);

    public ITenantRepository<DomainEntities.LoyaltyTransaction> LoyaltyTransactions =>
        _loyaltyTransactions ??= new TenantRepository<DomainEntities.LoyaltyTransaction>(_context);
    #endregion

    #region Settings
    public ITenantRepository<TenantEntities.TenantSetting> Settings =>
        _settings ??= new TenantRepository<TenantEntities.TenantSetting>(_context);

    public ITenantRepository<TenantEntities.WebPushSubscription> WebPushSubscriptions =>
        _webPushSubscriptions ??= new TenantRepository<TenantEntities.WebPushSubscription>(_context);
    #endregion

    #region Stores & Inventory
    public ITenantRepository<TenantEntities.Store> Stores =>
        _stores ??= new TenantRepository<TenantEntities.Store>(_context);

    public ITenantRepository<TenantEntities.ProductStoreStock> ProductStoreStock =>
        _productStoreStock ??= new TenantRepository<TenantEntities.ProductStoreStock>(_context);
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
