using Microsoft.EntityFrameworkCore;
using TenantEntity = CC.Domain.Tenancy.Tenant;
using TenantProvisioningEntity = CC.Domain.Tenancy.TenantProvisioning;

namespace CC.Infraestructure.AdminDb
{
    /// <summary>
    /// DbContext para la base de datos de administración (Admin DB)
    /// Contiene información de todos los tenants y su aprovisionamiento
    /// </summary>
    public class AdminDbContext : DbContext
    {
        public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
        {
        }

        public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
        public DbSet<TenantProvisioningEntity> TenantProvisionings => Set<TenantProvisioningEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Aplicar configuraciones desde archivos separados
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
        }
    }
}
