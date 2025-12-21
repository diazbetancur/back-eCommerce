using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.EF;
using CC.Infraestructure.Sql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AdminTenant = CC.Infraestructure.Admin.Entities.Tenant;

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
        private readonly IMigrationRunner _migrationRunner;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TenantProvisioner> _logger;

        public TenantProvisioner(
            AdminDbContext adminDb,
            ITenantDatabaseCreator dbCreator,
            IMigrationRunner migrationRunner,
            IConfiguration configuration,
            ILogger<TenantProvisioner> logger)
        {
            _adminDb = adminDb;
            _dbCreator = dbCreator;
            _migrationRunner = migrationRunner;
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
                tenant.Status = TenantStatus.Ready;
                tenant.UpdatedAt = DateTime.UtcNow;
                await _adminDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully provisioned tenant {TenantId} ({Slug})", tenantId, tenant.Slug);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error provisioning tenant {TenantId} ({Slug})", tenantId, tenant.Slug);

                tenant.Status = TenantStatus.Failed;
                tenant.LastError = ex.Message;
                tenant.UpdatedAt = DateTime.UtcNow;
                await _adminDb.SaveChangesAsync(cancellationToken);

                return false;
            }
        }

        private async Task CreateDatabaseStepAsync(AdminTenant tenant, CancellationToken cancellationToken)
        {
            var step = new TenantProvisioning
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

        private async Task ApplyMigrationsStepAsync(AdminTenant tenant, CancellationToken cancellationToken)
        {
            var step = new TenantProvisioning
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

                var tenantConnectionString = GetTenantConnectionString(tenant.DbName);

                // Aplicar migraciones usando MigrationRunner
                var success = await _migrationRunner.ApplyTenantMigrationsAsync(tenantConnectionString, cancellationToken);

                if (!success)
                {
                    throw new Exception("Failed to apply tenant migrations");
                }

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

        private async Task SeedDataStepAsync(AdminTenant tenant, CancellationToken cancellationToken)
        {
            var step = new TenantProvisioning
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

                var tenantConnectionString = GetTenantConnectionString(tenant.DbName);

                // Aplicar seed de datos usando TenantDbSeeder
                await SeedTenantDataAsync(tenantConnectionString, tenant, cancellationToken);

                step.Status = "Success";
                step.CompletedAt = DateTime.UtcNow;
                step.Message = $"Tenant data seeded successfully. Admin email: admin@{tenant.Slug}";
                await _adminDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("? Data seeded for tenant {TenantId}", tenant.Id);
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

        private async Task SeedTenantDataAsync(string connectionString, AdminTenant tenant, CancellationToken cancellationToken)
        {
            var optionsBuilder = new DbContextOptionsBuilder<Tenant.TenantDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            await using var tenantDb = new Tenant.TenantDbContext(optionsBuilder.Options);

            // Usar el TenantDbSeeder para seed consistente
            await Tenant.TenantDbSeeder.SeedAsync(tenantDb, tenant.Slug, _logger);

            // ==================== SEED SETTINGS COMPLETOS ====================
            // Usa el nuevo TenantSettingsSeeder con configuración completa por defecto
            await TenantSeeders.TenantSettingsSeeder.SeedAsync(tenantDb, tenant.Slug, tenant.Name, _logger);

            // ==================== SEED ORDER STATUSES ====================
            if (!await tenantDb.OrderStatuses.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Seeding order statuses for tenant {TenantId}", tenant.Id);
                tenantDb.OrderStatuses.AddRange(
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "PENDING", Name = "Pending", Description = "Order placed, awaiting payment" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "PROCESSING", Name = "Processing", Description = "Payment received, order being prepared" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "SHIPPED", Name = "Shipped", Description = "Order has been shipped" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "DELIVERED", Name = "Delivered", Description = "Order delivered to customer" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "CANCELLED", Name = "Cancelled", Description = "Order cancelled" }
                );
                await tenantDb.SaveChangesAsync(cancellationToken);
            }

            _logger.LogWarning("??  Tenant {Slug} admin credentials - Email: admin@{Slug} | Password: TenantAdmin123!", tenant.Slug, tenant.Slug);
        }

        private string GetTenantConnectionString(string dbName)
        {
            var template = _configuration["Tenancy:TenantDbTemplate"]
                ?? throw new InvalidOperationException("Tenancy:TenantDbTemplate not configured");

            return template.Replace("{DbName}", dbName);
        }
    }
}
