using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Tenant
{
 public class TenantDbContext : DbContext
 {
 public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

 public DbSet<TenantUser> Users => Set<TenantUser>();
 public DbSet<TenantRole> Roles => Set<TenantRole>();
 public DbSet<TenantUserRole> UserRoles => Set<TenantUserRole>();
 public DbSet<TenantSetting> Settings => Set<TenantSetting>();
 public DbSet<WebPushSubscription> WebPushSubscriptions => Set<WebPushSubscription>();
 public DbSet<Product> Products => Set<Product>();
 public DbSet<Category> Categories => Set<Category>();
 public DbSet<Order> Orders => Set<Order>();

 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);
 modelBuilder.HasDefaultSchema("public");
 modelBuilder.Entity<TenantUser>().HasIndex(x => x.Email).IsUnique();
 modelBuilder.Entity<TenantSetting>().HasKey(x => x.Key);
 modelBuilder.Entity<TenantUserRole>().HasKey(x => new { x.UserId, x.RoleId });
 }
 }
}