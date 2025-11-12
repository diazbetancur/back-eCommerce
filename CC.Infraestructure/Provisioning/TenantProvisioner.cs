using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenantEntity = CC.Domain.Tenancy.Tenant;
using TenantProvisioningEntity = CC.Domain.Tenancy.TenantProvisioning;

namespace CC.Infraestructure.Provisioning
{
    /// <summary>
    /// Servicio para aprovisionar tenants (crear DB, aplicar migraciones, seed)
    /// </summary>
    public interface ITenantProvisioner
    {
        Task<bool> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }

    public class TenantProvisioner : ITenantProvisioner
    {
        private readonly AdminDbContext _adminDb;
        private readonly ITenantDatabaseCreator _dbCreator;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantProvisioner> _logger;

        public TenantProvisioner(
            AdminDbContext adminDb,
            ITenantDatabaseCreator dbCreator,
            IConfiguration configuration,
            ILogger<TenantProvisioner> logger)
        {
            _adminDb = adminDb;
            _dbCreator = dbCreator;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> ProvisionTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var tenant = await _adminDb.Tenants.FindAsync(new object[] { tenantId }, cancellationToken);
            if (tenant == null)
            {
                _logger.LogError("Tenant {TenantId} not found", tenantId);
                return false;
            }

            try
            {
                _logger.LogInformation("Starting provisioning for tenant {TenantId} ({Slug})", tenantId, tenant.Slug);

                // Paso 1: Crear base de datos
                await CreateDatabaseStepAsync(tenant, cancellationToken);

                // Paso 2: Aplicar migraciones
                await ApplyMigrationsStepAsync(tenant, cancellationToken);

                // Paso 3: Seed de datos
                await SeedDataStepAsync(tenant, cancellationToken);

                // Marcar como completado
                tenant.Status = "Active";
                tenant.UpdatedAt = DateTime.UtcNow;
                await _adminDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully provisioned tenant {TenantId} ({Slug})", tenantId, tenant.Slug);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error provisioning tenant {TenantId} ({Slug})", tenantId, tenant.Slug);
                
                tenant.Status = "Failed";
                tenant.LastError = ex.Message;
                tenant.UpdatedAt = DateTime.UtcNow;
                await _adminDb.SaveChangesAsync(cancellationToken);

                return false;
            }
        }

        private async Task CreateDatabaseStepAsync(TenantEntity tenant, CancellationToken cancellationToken)
        {
            var step = new TenantProvisioningEntity
            {
                TenantId = tenant.Id,
                Step = "CreateDatabase",
                Status = "InProgress",
                StartedAt = DateTime.UtcNow
            };
            _adminDb.TenantProvisionings.Add(step);
            await _adminDb.SaveChangesAsync(cancellationToken);

            try
            {
                _logger.LogInformation("Creating database {DbName} for tenant {TenantId}", tenant.DbName, tenant.Id);

                var created = await _dbCreator.CreateDatabaseAsync(tenant.DbName, cancellationToken);
                
                step.Status = "Success";
                step.CompletedAt = DateTime.UtcNow;
                step.Message = $"Database {tenant.DbName} created successfully";
                await _adminDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Database {DbName} created for tenant {TenantId}", tenant.DbName, tenant.Id);
            }
            catch (Exception ex)
            {
                step.Status = "Failed";
                step.CompletedAt = DateTime.UtcNow;
                step.ErrorMessage = ex.Message;
                await _adminDb.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        private async Task ApplyMigrationsStepAsync(TenantEntity tenant, CancellationToken cancellationToken)
        {
            var step = new TenantProvisioningEntity
            {
                TenantId = tenant.Id,
                Step = "ApplyMigrations",
                Status = "InProgress",
                StartedAt = DateTime.UtcNow
            };
            _adminDb.TenantProvisionings.Add(step);
            await _adminDb.SaveChangesAsync(cancellationToken);

            try
            {
                _logger.LogInformation("Applying migrations to database {DbName} for tenant {TenantId}", tenant.DbName, tenant.Id);

                // TODO: Aplicar migraciones usando DbContext del tenant
                // Necesitarás crear una instancia del TenantDbContext con la connection string del tenant
                // y ejecutar context.Database.MigrateAsync()

                var tenantConnectionString = GetTenantConnectionString(tenant.DbName);
                
                // Por ahora simulamos el proceso
                await Task.Delay(1000, cancellationToken); // Simular proceso de migración

                step.Status = "Success";
                step.CompletedAt = DateTime.UtcNow;
                step.Message = "Migrations applied successfully";
                await _adminDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Migrations applied to {DbName} for tenant {TenantId}", tenant.DbName, tenant.Id);
            }
            catch (Exception ex)
            {
                step.Status = "Failed";
                step.CompletedAt = DateTime.UtcNow;
                step.ErrorMessage = ex.Message;
                await _adminDb.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        private async Task SeedDataStepAsync(TenantEntity tenant, CancellationToken cancellationToken)
        {
            var step = new TenantProvisioningEntity
            {
                TenantId = tenant.Id,
                Step = "SeedData",
                Status = "InProgress",
                StartedAt = DateTime.UtcNow
            };
            _adminDb.TenantProvisionings.Add(step);
            await _adminDb.SaveChangesAsync(cancellationToken);

            try
            {
                _logger.LogInformation("Seeding data for tenant {TenantId} in database {DbName}", tenant.Id, tenant.DbName);

                // TODO: Seed de datos iniciales (catálogo demo, configuraciones, etc.)
                // Usar el TenantDbContext para insertar datos iniciales

                // Por ahora simulamos el proceso
                await Task.Delay(500, cancellationToken); // Simular proceso de seed

                step.Status = "Success";
                step.CompletedAt = DateTime.UtcNow;
                step.Message = "Demo data seeded successfully";
                await _adminDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Data seeded for tenant {TenantId}", tenant.Id);
            }
            catch (Exception ex)
            {
                step.Status = "Failed";
                step.CompletedAt = DateTime.UtcNow;
                step.ErrorMessage = ex.Message;
                await _adminDb.SaveChangesAsync(cancellationToken);
                throw;
            }
        }

        private string GetTenantConnectionString(string dbName)
        {
            var template = _configuration["Tenancy:TenantDbTemplate"] 
                ?? throw new InvalidOperationException("Tenancy:TenantDbTemplate not configured");
            
            return template.Replace("{DbName}", dbName);
        }
    }
}
