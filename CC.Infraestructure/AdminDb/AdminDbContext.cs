using Microsoft.EntityFrameworkCore;
using CC.Infraestructure.Admin.Entities;
using TenantEntity = CC.Infraestructure.Admin.Entities.Tenant;

namespace CC.Infraestructure.AdminDb
{
    /// <summary>
    /// DbContext para la base de datos de administraci�n (Admin DB)
    /// Contiene informaci�n de todos los tenants y su aprovisionamiento
    /// </summary>
    public class AdminDbContext : DbContext
    {
        public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
        {
        }

        // Entidades de tenants y planes
        public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
        public DbSet<TenantProvisioning> TenantProvisionings => Set<TenantProvisioning>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<Feature> Features => Set<Feature>();
        public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
        public DbSet<PlanLimit> PlanLimits => Set<PlanLimit>();  // ? NUEVO
        public DbSet<TenantFeatureOverride> TenantFeatureOverrides => Set<TenantFeatureOverride>();
        public DbSet<TenantUsageDaily> TenantUsageDaily => Set<TenantUsageDaily>();

        // Entidades de administraci�n
        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
        public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
        public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
        public DbSet<AdminPermission> AdminPermissions => Set<AdminPermission>();
        public DbSet<AdminRolePermission> AdminRolePermissions => Set<AdminRolePermission>();
        public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("admin");

            // ==================== TENANTS & PLANS ====================
            modelBuilder.Entity<Plan>().HasIndex(p => p.Code).IsUnique();
            modelBuilder.Entity<Feature>().HasIndex(f => f.Code).IsUnique();
            modelBuilder.Entity<TenantEntity>().HasIndex(t => t.Slug).IsUnique();

            modelBuilder.Entity<PlanFeature>().HasKey(x => new { x.PlanId, x.FeatureId });
            modelBuilder.Entity<TenantFeatureOverride>().HasKey(x => new { x.TenantId, x.FeatureId });
            modelBuilder.Entity<TenantUsageDaily>().HasKey(x => new { x.TenantId, x.Date });

            // ? NUEVO: Configurar PlanLimit
            modelBuilder.Entity<PlanLimit>(entity =>
            {
                entity.ToTable("PlanLimits", "admin");
                entity.HasKey(e => e.Id);

                // �ndice �nico: un plan no puede tener m�ltiples l�mites con el mismo c�digo
                entity.HasIndex(e => new { e.PlanId, e.LimitCode })
                    .IsUnique()
                    .HasDatabaseName("UQ_PlanLimits_PlanId_LimitCode");

                entity.Property(e => e.PlanId).IsRequired();
                entity.Property(e => e.LimitCode).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LimitValue).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).IsRequired();

                entity.HasOne(pl => pl.Plan)
                    .WithMany(p => p.Limits)
                    .HasForeignKey(pl => pl.PlanId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configurar relaci�n TenantProvisioning -> Tenant
            modelBuilder.Entity<TenantProvisioning>()
                .HasOne(tp => tp.Tenant)
                .WithMany()
                .HasForeignKey(tp => tp.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            // ==================== ADMIN USERS & ROLES ====================
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
                entity.Property(e => e.IsSystemRole).IsRequired();
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

            // AdminPermission
            modelBuilder.Entity<AdminPermission>(entity =>
            {
                entity.ToTable("AdminPermissions", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Resource).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.IsSystemPermission).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
            });

            // AdminRolePermission (Many-to-Many)
            modelBuilder.Entity<AdminRolePermission>(entity =>
            {
                entity.ToTable("AdminRolePermissions", "admin");
                entity.HasKey(e => new { e.AdminRoleId, e.AdminPermissionId });

                entity.HasOne(e => e.AdminRole)
                    .WithMany(r => r.RolePermissions)
                    .HasForeignKey(e => e.AdminRoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AdminPermission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(e => e.AdminPermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.AssignedAt).IsRequired();
            });

            // AdminAuditLog
            modelBuilder.Entity<AdminAuditLog>(entity =>
            {
                entity.ToTable("AdminAuditLogs", "admin");
                entity.HasKey(e => e.Id);

                // Índices para consultas frecuentes
                entity.HasIndex(e => e.AdminUserId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.ResourceType);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.AdminUserId, e.CreatedAt });
                entity.HasIndex(e => new { e.ResourceType, e.ResourceId });

                entity.Property(e => e.AdminUserId).IsRequired();
                entity.Property(e => e.AdminUserEmail).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ResourceId).HasMaxLength(100);
                entity.Property(e => e.IpAddress).HasMaxLength(45);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).IsRequired();

                // Relación con AdminUser (opcional - puede ser null si el usuario fue eliminado)
                entity.HasOne(e => e.AdminUser)
                    .WithMany()
                    .HasForeignKey(e => e.AdminUserId)
                    .OnDelete(DeleteBehavior.Restrict); // No eliminar logs si se elimina usuario
            });
        }
    }
}
