using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Tenant
{
    /// <summary>
    /// DbContext para bases de datos de tenants individuales
    /// Contiene el esquema de negocio: catálogo, carrito, pedidos, usuarios
    /// </summary>
    public class TenantDbContext : DbContext
    {
        public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

        #region Authentication & Authorization
        public DbSet<TenantUser> Users => Set<TenantUser>();
        public DbSet<TenantRole> Roles => Set<TenantRole>();
        public DbSet<TenantUserRole> UserRoles => Set<TenantUserRole>();
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

            #region Authentication & Authorization
            modelBuilder.Entity<TenantUser>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            modelBuilder.Entity<TenantRole>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            });

            modelBuilder.Entity<TenantUserRole>(entity =>
            {
                entity.ToTable("UserRoles");
                entity.HasKey(e => new { e.UserId, e.RoleId });
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
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Price).HasPrecision(18, 2);
            });

            modelBuilder.Entity<ProductCategory>(entity =>
            {
                entity.ToTable("ProductCategories");
                entity.HasKey(e => new { e.ProductId, e.CategoryId });
            });

            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.ToTable("ProductImages");
                entity.HasKey(e => e.Id);
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