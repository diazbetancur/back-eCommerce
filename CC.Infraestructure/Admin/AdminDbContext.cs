using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Admin
{
 public class AdminDbContext : DbContext
 {
 public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

 public DbSet<CC.Infraestructure.Admin.Entities.Tenant> Tenants => Set<CC.Infraestructure.Admin.Entities.Tenant>();
 public DbSet<Plan> Plans => Set<Plan>();
 public DbSet<Feature> Features => Set<Feature>();
 public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
 public DbSet<TenantFeatureOverride> TenantFeatureOverrides => Set<TenantFeatureOverride>();
 public DbSet<TenantUsageDaily> TenantUsageDaily => Set<TenantUsageDaily>();
 public DbSet<TenantProvisioning> TenantProvisionings => Set<TenantProvisioning>();

 protected override void OnModelCreating(ModelBuilder modelBuilder)
 {
 base.OnModelCreating(modelBuilder);

 modelBuilder.HasDefaultSchema("admin");

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
 }
 }
}