using CC.Domain.Favorites;
using CC.Domain.Loyalty;
using CC.Domain.Users;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Tenant
{
  /// <summary>
  /// DbContext para bases de datos de tenants individuales
  /// Contiene el esquema de negocio: cat�logo, carrito, pedidos, usuarios
  /// </summary>
  public class TenantDbContext : DbContext
  {
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    #region User Authentication & Authorization
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<RoleModulePermission> RoleModulePermissions => Set<RoleModulePermission>();
    #endregion

    #region Favorites
    public DbSet<FavoriteProduct> FavoriteProducts => Set<FavoriteProduct>();
    #endregion

    #region Loyalty Program
    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    #endregion

    #region Settings & Configuration
    public DbSet<TenantSetting> Settings => Set<TenantSetting>();
    public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();
    #endregion

    #region Catalog
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Banner> Banners => Set<Banner>();
    #endregion

    #region Shopping Cart
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    #endregion

    #region Orders
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatus> OrderStatuses => Set<OrderStatus>();
    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      // Schema por defecto
      modelBuilder.HasDefaultSchema("public");

      #region User Authentication & Authorization
      modelBuilder.Entity<User>(entity =>
      {
        entity.ToTable("Users");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Email).IsUnique();
        entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
        entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
        entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.PhoneNumber).HasMaxLength(20);
        entity.Property(e => e.IsActive).IsRequired();
        entity.Property(e => e.MustChangePassword).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
      });

      modelBuilder.Entity<Role>(entity =>
      {
        entity.ToTable("Roles");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Name).IsUnique();
        entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Description).HasMaxLength(255);
        entity.Property(e => e.CreatedAt).IsRequired();
      });

      modelBuilder.Entity<UserRole>(entity =>
      {
        entity.ToTable("UserRoles");
        entity.HasKey(e => new { e.UserId, e.RoleId });
        entity.Property(e => e.AssignedAt).IsRequired();

        entity.HasOne(ur => ur.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(ur => ur.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(ur => ur.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(ur => ur.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
      });

      modelBuilder.Entity<UserProfile>(entity =>
      {
        entity.ToTable("UserProfiles");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.PhoneNumber).HasMaxLength(50);
        entity.Property(e => e.DocumentType).HasMaxLength(50);
        entity.Property(e => e.DocumentNumber).HasMaxLength(100);
        entity.Property(e => e.Address).HasMaxLength(500);
        entity.Property(e => e.City).HasMaxLength(100);
        entity.Property(e => e.Country).HasMaxLength(100);
      });
      #endregion

      #region Favorites (New)
      modelBuilder.Entity<FavoriteProduct>(entity =>
      {
        entity.ToTable("FavoriteProducts");
        entity.HasKey(e => e.Id);

        // �ndice �nico compuesto: un usuario no puede tener el mismo producto favorito dos veces
        entity.HasIndex(e => new { e.UserId, e.ProductId })
                  .IsUnique()
                  .HasDatabaseName("UQ_FavoriteProducts_UserId_ProductId");

        // �ndices para b�squedas
        entity.HasIndex(e => e.UserId).HasDatabaseName("IX_FavoriteProducts_UserId");
        entity.HasIndex(e => e.ProductId).HasDatabaseName("IX_FavoriteProducts_ProductId");

        entity.Property(e => e.UserId).IsRequired();
        entity.Property(e => e.ProductId).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
      });
      #endregion

      #region Loyalty Program (New)
      modelBuilder.Entity<LoyaltyAccount>(entity =>
      {
        entity.ToTable("LoyaltyAccounts");
        entity.HasKey(e => e.Id);

        // �ndice �nico: un usuario solo puede tener una cuenta de loyalty
        entity.HasIndex(e => e.UserId)
                  .IsUnique()
                  .HasDatabaseName("UQ_LoyaltyAccounts_UserId");

        entity.Property(e => e.UserId).IsRequired();
        entity.Property(e => e.PointsBalance).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
        entity.Property(e => e.UpdatedAt).IsRequired();

        // Relaci�n 1:N con transacciones
        entity.HasMany(a => a.Transactions)
                  .WithOne(t => t.LoyaltyAccount)
                  .HasForeignKey(t => t.LoyaltyAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
      });

      modelBuilder.Entity<LoyaltyTransaction>(entity =>
      {
        entity.ToTable("LoyaltyTransactions");
        entity.HasKey(e => e.Id);

        // �ndices
        entity.HasIndex(e => e.LoyaltyAccountId).HasDatabaseName("IX_LoyaltyTransactions_AccountId");
        entity.HasIndex(e => e.OrderId).HasDatabaseName("IX_LoyaltyTransactions_OrderId");
        entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_LoyaltyTransactions_CreatedAt");

        // �ndice �nico: una orden solo puede generar puntos una vez
        entity.HasIndex(e => e.OrderId)
                  .IsUnique()
                  .HasFilter("\"OrderId\" IS NOT NULL")
                  .HasDatabaseName("UQ_LoyaltyTransactions_OrderId");

        entity.Property(e => e.LoyaltyAccountId).IsRequired();
        entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Points).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.CreatedAt).IsRequired();
      });
      #endregion

      #region Modules & Permissions
      modelBuilder.Entity<Module>(entity =>
      {
        entity.ToTable("Modules");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Code).IsUnique();
        entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.IconName).HasMaxLength(50);
        entity.Property(e => e.IsActive).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
      });

      modelBuilder.Entity<RoleModulePermission>(entity =>
      {
        entity.ToTable("RoleModulePermissions");
        entity.HasKey(e => e.Id);

        // �ndice �nico: un rol no puede tener m�ltiples permisos para el mismo m�dulo
        entity.HasIndex(e => new { e.RoleId, e.ModuleId })
                  .IsUnique()
                  .HasDatabaseName("UQ_RoleModulePermissions_RoleId_ModuleId");

        entity.Property(e => e.RoleId).IsRequired();
        entity.Property(e => e.ModuleId).IsRequired();
        entity.Property(e => e.CanView).IsRequired();
        entity.Property(e => e.CanCreate).IsRequired();
        entity.Property(e => e.CanUpdate).IsRequired();
        entity.Property(e => e.CanDelete).IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();

        entity.HasOne(p => p.Role)
                  .WithMany(r => r.ModulePermissions)
                  .HasForeignKey(p => p.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(p => p.Module)
                  .WithMany(m => m.RolePermissions)
                  .HasForeignKey(p => p.ModuleId)
                  .OnDelete(DeleteBehavior.Cascade);
      });
      #endregion

      #region Settings
      modelBuilder.Entity<TenantSetting>(entity =>
      {
        entity.ToTable("Settings");
        entity.HasKey(e => e.Key);
        entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Value).IsRequired();
      });

      modelBuilder.Entity<WebPushSubscription>(entity =>
      {
        entity.ToTable("WebPushSubscriptions");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Endpoint).IsUnique();
      });
      #endregion

      #region Catalog
      modelBuilder.Entity<Category>(entity =>
      {
        entity.ToTable("Categories");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Slug).IsRequired().HasMaxLength(200);
        entity.HasIndex(e => e.Slug).IsUnique();
        entity.HasIndex(e => e.ParentId);
        entity.HasIndex(e => e.DisplayOrder);

        // Relación jerárquica (self-referencing)
        entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
      });

      modelBuilder.Entity<Product>(entity =>
      {
        entity.ToTable("Products");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Slug).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Sku).HasMaxLength(100);
        entity.Property(e => e.Price).HasPrecision(18, 2);
        entity.Property(e => e.CompareAtPrice).HasPrecision(18, 2);
        entity.Property(e => e.Tags).HasMaxLength(500);
        entity.Property(e => e.Brand).HasMaxLength(100);

        entity.HasIndex(e => e.Slug).IsUnique();
        entity.HasIndex(e => e.Sku);
        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.IsFeatured);
        // Índice para búsqueda full-text (typeahead)
        entity.HasIndex(e => e.Name);
        entity.HasIndex(e => e.Tags);
      });

      modelBuilder.Entity<ProductCategory>(entity =>
      {
        entity.ToTable("ProductCategories");
        entity.HasKey(e => new { e.ProductId, e.CategoryId });

        entity.HasOne<Product>()
                  .WithMany(p => p.Categories)
                  .HasForeignKey(pc => pc.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<Category>()
                  .WithMany(c => c.Products)
                  .HasForeignKey(pc => pc.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
      });

      modelBuilder.Entity<ProductImage>(entity =>
      {
        entity.ToTable("ProductImages");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.ProductId);

        entity.HasOne<Product>()
                  .WithMany(p => p.Images)
                  .HasForeignKey(pi => pi.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
      });

      modelBuilder.Entity<Banner>(entity =>
      {
        entity.ToTable("Banners");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
        entity.Property(e => e.ImageUrlDesktop).IsRequired().HasMaxLength(500);
        entity.Property(e => e.ImageUrlMobile).HasMaxLength(500);
        entity.Property(e => e.TargetUrl).HasMaxLength(500);
        entity.Property(e => e.ButtonText).HasMaxLength(100);

        entity.HasIndex(e => e.IsActive);
        entity.HasIndex(e => e.Position);
        entity.HasIndex(e => e.DisplayOrder);
      });
      #endregion

      #region Shopping Cart
      modelBuilder.Entity<Cart>(entity =>
      {
        entity.ToTable("Carts");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.UserId);
      });

      modelBuilder.Entity<CartItem>(entity =>
      {
        entity.ToTable("CartItems");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.CartId);
      });
      #endregion

      #region Orders
      modelBuilder.Entity<Order>(entity =>
      {
        entity.ToTable("Orders");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.UserId);
        entity.HasIndex(e => e.SessionId);
        entity.HasIndex(e => e.IdempotencyKey).IsUnique();
        entity.HasIndex(e => e.OrderNumber).IsUnique();
        entity.Property(e => e.Total).HasPrecision(18, 2);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
        entity.Property(e => e.Tax).HasPrecision(18, 2);
        entity.Property(e => e.Shipping).HasPrecision(18, 2);
        entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
        entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
        entity.Property(e => e.ShippingAddress).IsRequired();
        entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
      });

      modelBuilder.Entity<OrderItem>(entity =>
      {
        entity.ToTable("OrderItems");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.OrderId);
        entity.Property(e => e.Price).HasPrecision(18, 2);
        entity.Property(e => e.Subtotal).HasPrecision(18, 2);
      });

      modelBuilder.Entity<OrderStatus>(entity =>
      {
        entity.ToTable("OrderStatuses");
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Code).IsUnique();
      });
      #endregion
    }
  }
}