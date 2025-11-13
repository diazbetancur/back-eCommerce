using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.Tenant
{
    /// <summary>
    /// Factory para crear instancias de TenantDbContext usando la conexión del tenant resuelto
    /// </summary>
    public class TenantDbContextFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TenantDbContextFactory> _logger;

        public TenantDbContextFactory(
            IServiceProvider serviceProvider,
            ILogger<TenantDbContextFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Crea un DbContext del tenant usando la conexión resuelta del contexto actual
        /// </summary>
        public TenantDbContext Create()
        {
            var tenantAccessor = _serviceProvider.GetRequiredService<ITenantAccessor>();

            if (!tenantAccessor.HasTenant || tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException(
                    "Cannot create TenantDbContext: No tenant has been resolved in the current request context. " +
                    "Ensure TenantResolutionMiddleware is properly configured and a valid X-Tenant-Slug header is provided.");
            }

            var tenantInfo = tenantAccessor.TenantInfo;

            _logger.LogDebug(
                "Creating TenantDbContext for tenant {Slug} (DbName: {DbName})",
                tenantInfo.Slug, tenantInfo.DbName);

            return CreateWithConnectionString(tenantInfo.ConnectionString);
        }

        /// <summary>
        /// Crea un DbContext del tenant con una connection string específica
        /// Útil para operaciones fuera del contexto de request (workers, etc.)
        /// </summary>
        public TenantDbContext Create(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            }

            return CreateWithConnectionString(connectionString);
        }

        private TenantDbContext CreateWithConnectionString(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            // Opcional: Configurar logging sensible
            optionsBuilder.EnableSensitiveDataLogging(false);
            optionsBuilder.EnableDetailedErrors(true);

            return new TenantDbContext(optionsBuilder.Options);
        }
    }
}