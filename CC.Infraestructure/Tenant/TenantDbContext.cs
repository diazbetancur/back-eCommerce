using CC.Domain.Favorites;
using CC.Domain.Loyalty;
using CC.Domain.Users;
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

        #region User Authentication (New)
        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        #endregion

        #region Favorites (New)
        public DbSet<FavoriteProduct> FavoriteProducts => Set<FavoriteProduct>();
        #endregion

        #region Loyalty Program (New)
        public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();
        public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
        #endregion

        #region Authentication & Authorization (Legacy)
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

            #region User Authentication (New)
            modelBuilder.Entity<UserAccount>(entity =>
            {
                entity.ToTable("UserAccounts");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
                entity.Property(e => e.PasswordSalt).IsRequired().HasMaxLength(500);
                entity.Property(e => e.IsActive).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();

                // Relación 1:1 con UserProfile
                entity.HasOne(u => u.Profile)
                    .WithOne(p => p.UserAccount)
                    .HasForeignKey<UserProfile>(p => p.Id)
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
                
                // Índice único compuesto: un usuario no puede tener el mismo producto favorito dos veces
                entity.HasIndex(e => new { e.UserId, e.ProductId })
                    .IsUnique()
                    .HasDatabaseName("UQ_FavoriteProducts_UserId_ProductId");
                
                // Índices para búsquedas
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
                
                // Índice único: un usuario solo puede tener una cuenta de loyalty
                entity.HasIndex(e => e.UserId)
                    .IsUnique()
                    .HasDatabaseName("UQ_LoyaltyAccounts_UserId");
                
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.PointsBalance).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();

                // Relación 1:N con transacciones
                entity.HasMany(a => a.Transactions)
                    .WithOne(t => t.LoyaltyAccount)
                    .HasForeignKey(t => t.LoyaltyAccountId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<LoyaltyTransaction>(entity =>
            {
                entity.ToTable("LoyaltyTransactions");
                entity.HasKey(e => e.Id);
                
                // Índices
                entity.HasIndex(e => e.LoyaltyAccountId).HasDatabaseName("IX_LoyaltyTransactions_AccountId");
                entity.HasIndex(e => e.OrderId).HasDatabaseName("IX_LoyaltyTransactions_OrderId");
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_LoyaltyTransactions_CreatedAt");
                
                // Índice único: una orden solo puede generar puntos una vez
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

            #region Authentication & Authorization (Legacy)
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