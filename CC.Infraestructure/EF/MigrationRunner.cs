using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.EF
{
    /// <summary>
    /// Helper para aplicar migraciones de EF Core en runtime
    /// Útil para aplicar migraciones a bases de datos de tenants durante el aprovisionamiento
    /// </summary>
    public interface IMigrationRunner
    {
        /// <summary>
        /// Aplica migraciones de AdminDb
        /// </summary>
        Task<bool> ApplyAdminMigrationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Aplica migraciones de TenantDb usando una connection string específica
        /// </summary>
        Task<bool> ApplyTenantMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifica si hay migraciones pendientes para AdminDb
        /// </summary>
        Task<bool> HasPendingAdminMigrationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifica si hay migraciones pendientes para TenantDb
        /// </summary>
        Task<bool> HasPendingTenantMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);
    }

    public class MigrationRunner : IMigrationRunner
    {
        private readonly AdminDbContext _adminDb;
        private readonly ILogger<MigrationRunner> _logger;

        public MigrationRunner(AdminDbContext adminDb, ILogger<MigrationRunner> logger)
        {
            _adminDb = adminDb;
            _logger = logger;
        }

        public async Task<bool> ApplyAdminMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Starting Admin DB migrations...");

                var pendingMigrations = await _adminDb.Database.GetPendingMigrationsAsync(cancellationToken);
                var pendingCount = pendingMigrations.Count();

                if (pendingCount == 0)
                {
                    _logger.LogInformation("Admin DB is up to date. No migrations to apply.");
                    return true;
                }

                _logger.LogInformation("Applying {Count} pending migrations to Admin DB", pendingCount);

                await _adminDb.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("Admin DB migrations applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying Admin DB migrations");
                return false;
            }
        }

        public async Task<bool> ApplyTenantMigrationsAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            }

            try
            {
                _logger.LogInformation("Starting Tenant DB migrations for connection: {ConnectionStringPreview}",
                    GetConnectionStringPreview(connectionString));

                // Crear DbContext con la connection string específica
                var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
                optionsBuilder.UseNpgsql(connectionString);

                await using var tenantDb = new TenantDbContext(optionsBuilder.Options);

                // Verificar conexión
                if (!await tenantDb.Database.CanConnectAsync(cancellationToken))
                {
                    _logger.LogError("Cannot connect to Tenant DB");
                    return false;
                }

                var pendingMigrations = await tenantDb.Database.GetPendingMigrationsAsync(cancellationToken);
                var pendingCount = pendingMigrations.Count();

                if (pendingCount == 0)
                {
                    _logger.LogInformation("Tenant DB is up to date. No migrations to apply.");
                    return true;
                }

                _logger.LogInformation("Applying {Count} pending migrations to Tenant DB", pendingCount);

                // Aplicar migraciones
                await tenantDb.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("Tenant DB migrations applied successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying Tenant DB migrations");
                return false;
            }
        }

        public async Task<bool> HasPendingAdminMigrationsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var pendingMigrations = await _adminDb.Database.GetPendingMigrationsAsync(cancellationToken);
                return pendingMigrations.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Admin DB pending migrations");
                return false;
            }
        }

        public async Task<bool> HasPendingTenantMigrationsAsync(string connectionString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
            }

            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
                optionsBuilder.UseNpgsql(connectionString);

                await using var tenantDb = new TenantDbContext(optionsBuilder.Options);
                var pendingMigrations = await tenantDb.Database.GetPendingMigrationsAsync(cancellationToken);
                return pendingMigrations.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Tenant DB pending migrations");
                return false;
            }
        }

        /// <summary>
        /// Obtiene una preview segura de la connection string (sin contraseña)
        /// </summary>
        private static string GetConnectionStringPreview(string connectionString)
        {
            try
            {
                var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
                return $"Host={builder.Host};Database={builder.Database}";
            }
            catch
            {
                return "[Connection string preview unavailable]";
            }
        }
    }
}
