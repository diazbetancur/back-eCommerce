using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CC.Infraestructure.DesignTime
{
    public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
    {
        public TenantDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            
            // Connection string temporal para crear migraciones (NO se usa en runtime)
            optionsBuilder.UseNpgsql("Host=localhost;Database=tenant_template;Username=postgres;Password=postgres");

            return new TenantDbContext(optionsBuilder.Options);
        }
    }
}
