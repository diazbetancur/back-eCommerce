using CC.Infraestructure.AdminDb;
using CC.Infraestructure.EF;
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

                var tenantConnectionString = GetTenantConnectionString(tenant.DbName);
                
                // Aplicar seed de datos
                await SeedTenantDataAsync(tenantConnectionString, tenant, cancellationToken);

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

        private async Task SeedTenantDataAsync(string connectionString, TenantEntity tenant, CancellationToken cancellationToken)
        {
            var optionsBuilder = new DbContextOptionsBuilder<Tenant.TenantDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            await using var db = new Tenant.TenantDbContext(optionsBuilder.Options);

            // Seed Roles
            if (!await db.Roles.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Seeding roles for tenant {TenantId}", tenant.Id);
                db.Roles.AddRange(
                    new Tenant.Entities.TenantRole { Id = Guid.NewGuid(), Name = "Admin" },
                    new Tenant.Entities.TenantRole { Id = Guid.NewGuid(), Name = "Manager" },
                    new Tenant.Entities.TenantRole { Id = Guid.NewGuid(), Name = "Customer" }
                );
                await db.SaveChangesAsync(cancellationToken);
            }

            // Seed Admin User
            if (!await db.Users.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Seeding admin user for tenant {TenantId}", tenant.Id);
                var tempPassword = Guid.NewGuid().ToString("N")[..10];
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
                var hash = hasher.HashPassword(null!, tempPassword);

                db.Users.Add(new Tenant.Entities.TenantUser
                {
                    Id = Guid.NewGuid(),
                    Email = $"admin@{tenant.Slug}.local",
                    PasswordHash = hash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogWarning("Admin user created for tenant {TenantId}. Email: admin@{Slug}.local, Temp Password: {Password}",
                    tenant.Id, tenant.Slug, tempPassword);
            }

            // Seed Settings
            if (!await db.Settings.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Seeding settings for tenant {TenantId}", tenant.Id);
                db.Settings.AddRange(
                    new Tenant.Entities.TenantSetting { Key = "Currency", Value = "USD" },
                    new Tenant.Entities.TenantSetting { Key = "TaxRate", Value = "0.15" },
                    new Tenant.Entities.TenantSetting { Key = "StoreName", Value = tenant.Name }
                );
                await db.SaveChangesAsync(cancellationToken);
            }

            // Seed Order Statuses
            if (!await db.OrderStatuses.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Seeding order statuses for tenant {TenantId}", tenant.Id);
                db.OrderStatuses.AddRange(
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "PENDING", Name = "Pending", Description = "Order placed, awaiting payment" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "PROCESSING", Name = "Processing", Description = "Payment received, order being prepared" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "SHIPPED", Name = "Shipped", Description = "Order has been shipped" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "DELIVERED", Name = "Delivered", Description = "Order delivered to customer" },
                    new Tenant.Entities.OrderStatus { Id = Guid.NewGuid(), Code = "CANCELLED", Name = "Cancelled", Description = "Order cancelled" }
                );
                await db.SaveChangesAsync(cancellationToken);
            }

            // Seed Demo Categories (opcional)
            if (!await db.Categories.AnyAsync(cancellationToken) && tenant.Plan != "Basic")
            {
                _logger.LogInformation("Seeding demo categories for tenant {TenantId}", tenant.Id);
                db.Categories.AddRange(
                    new Tenant.Entities.Category { Id = Guid.NewGuid(), Name = "Electronics" },
                    new Tenant.Entities.Category { Id = Guid.NewGuid(), Name = "Clothing" },
                    new Tenant.Entities.Category { Id = Guid.NewGuid(), Name = "Books" }
                );
                await db.SaveChangesAsync(cancellationToken);
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
