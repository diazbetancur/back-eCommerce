using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Admin
{
 public class AdminDbContext : DbContext
 {
 public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

 // Tenants y Planes
 public DbSet<CC.Infraestructure.Admin.Entities.Tenant> Tenants => Set<CC.Infraestructure.Admin.Entities.Tenant>();
 public DbSet<Plan> Plans => Set<Plan>();
 public DbSet<Feature> Features => Set<Feature>();
 public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
 public DbSet<TenantFeatureOverride> TenantFeatureOverrides => Set<TenantFeatureOverride>();
 public DbSet<TenantUsageDaily> TenantUsageDaily => Set<TenantUsageDaily>();
 public DbSet<TenantProvisioning> TenantProvisionings => Set<TenantProvisioning>();

 // Usuarios Administrativos (NUEVO)
 public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
 public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
 public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();

 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);

 modelBuilder.HasDefaultSchema("admin");

 #region Tenants & Plans
 modelBuilder.Entity<Plan>().HasIndex(p => p.Code).IsUnique();
 modelBuilder.Entity<Feature>().HasIndex(f => f.Code).IsUnique();
 modelBuilder.Entity<CC.Infraestructure.Admin.Entities.Tenant>().HasIndex(t => t.Slug).IsUnique();

 modelBuilder.Entity<PlanFeature>().HasKey(x => new { x.PlanId, x.FeatureId });
 modelBuilder.Entity<TenantFeatureOverride>().HasKey(x => new { x.TenantId, x.FeatureId });
 modelBuilder.Entity<TenantUsageDaily>().HasKey(x => new { x.TenantId, x.Date });
 
 // Configurar relación TenantProvisioning -> Tenant
 modelBuilder.Entity<TenantProvisioning>()
 .HasOne(tp => tp.Tenant)
 .WithMany()
 .HasForeignKey(tp => tp.TenantId)
 .OnDelete(DeleteBehavior.Cascade);
 #endregion

 #region Admin Users & Roles (NUEVO)
 // AdminUser
 modelBuilder.Entity<AdminUser>(entity =>
 {
 entity.ToTable("AdminUsers", "admin");
 entity.HasKey(e => e.Id);
 entity.HasIndex(e => e.Email).IsUnique();
 entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
 entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
 entity.Property(e => e.PasswordSalt).IsRequired().HasMaxLength(500);
 entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
 entity.Property(e => e.IsActive).IsRequired();
 entity.Property(e => e.CreatedAt).IsRequired();
 });

 // AdminRole
 modelBuilder.Entity<AdminRole>(entity =>
 {
 entity.ToTable("AdminRoles", "admin");
 entity.HasKey(e => e.Id);
 entity.HasIndex(e => e.Name).IsUnique();
 entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
 entity.Property(e => e.Description).HasMaxLength(500);
 entity.Property(e => e.CreatedAt).IsRequired();
 });

 // AdminUserRole (Many-to-Many)
 modelBuilder.Entity<AdminUserRole>(entity =>
 {
 entity.ToTable("AdminUserRoles", "admin");
 entity.HasKey(e => new { e.AdminUserId, e.AdminRoleId });
 
 entity.HasOne(e => e.AdminUser)
 .WithMany(u => u.UserRoles)
 .HasForeignKey(e => e.AdminUserId)
 .OnDelete(DeleteBehavior.Cascade);
 
 entity.HasOne(e => e.AdminRole)
 .WithMany(r => r.UserRoles)
 .HasForeignKey(e => e.AdminRoleId)
 .OnDelete(DeleteBehavior.Cascade);

 entity.Property(e => e.AssignedAt).IsRequired();
 });
 #endregion
 }
 }
}