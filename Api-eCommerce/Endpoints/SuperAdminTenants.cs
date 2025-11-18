using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using CC.Infraestructure.TenantSeeders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.RegularExpressions;

namespace Api_eCommerce.Endpoints
{
    public static class SuperAdminTenants
    {
        public static IEndpointRouteBuilder MapSuperAdminTenants(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/superadmin/tenants");

            group.MapPost("/", CreateTenant);
            group.MapPost("/repair", RepairTenant);

            return app;
        }

        private static async Task<IResult> CreateTenant(
            AdminDbContext adminDb,
            ITenantConnectionProtector protector,
            TenantDbContextFactory factory,
            ILoggerFactory loggerFactory,
            string slug,
            string name,
            string planCode)
        {
            var logger = loggerFactory.CreateLogger("SuperAdminTenants");
            var regex = new Regex("^[a-z0-9-]{3,}$");
            
            if (!regex.IsMatch(slug))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { { "slug", new[] { "invalid format" } } });
            }

            var plan = await adminDb.Plans.FirstOrDefaultAsync(p => p.Code == planCode);
            if (plan == null)
            {
                return Results.NotFound(new { errors = "plan not found" });
            }

            var tenant = new CC.Infraestructure.Admin.Entities.Tenant
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Name = name,
                Status = TenantStatus.Pending,
                PlanId = plan.Id,
                CreatedAt = DateTime.UtcNow
            };
            
            adminDb.Tenants.Add(tenant);
            await adminDb.SaveChangesAsync();

            try
            {
                // 1. Crear base de datos
                var adminConn = adminDb.Database.GetConnectionString();
                var csb = new NpgsqlConnectionStringBuilder(adminConn);
                var dbName = $"tenant_{slug}";
                var masterCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Database = "postgres" }.ToString();
                
                await using (var conn = new NpgsqlConnection(masterCs))
                {
                    await conn.OpenAsync();
                    await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\" WITH TEMPLATE template0 ENCODING 'UTF8';", conn);
                    try
                    {
                        await cmd.ExecuteNonQueryAsync();
                        logger.LogInformation("? Created database: {DbName}", dbName);
                    }
                    catch (PostgresException pgEx) when (pgEx.SqlState == "42P04")
                    {
                        logger.LogWarning("??  Database {DbName} already exists", dbName);
                    }
                }

                tenant.Status = TenantStatus.Seeding;
                await adminDb.SaveChangesAsync();

                // 2. Aplicar migraciones y seed
                var tenantCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Database = dbName }.ToString();
                await using (var tenantDb = factory.Create(tenantCs))
                {
                    // Aplicar migraciones
                    await tenantDb.Database.MigrateAsync();
                    logger.LogInformation("? Migrations applied");

                    // Seed roles
                    if (!await tenantDb.Roles.AnyAsync())
                    {
                        var roles = new[]
                        {
                            new TenantRole { Id = Guid.NewGuid(), Name = "Admin" },
                            new TenantRole { Id = Guid.NewGuid(), Name = "Manager" },
                            new TenantRole { Id = Guid.NewGuid(), Name = "Viewer" }
                        };
                        tenantDb.Roles.AddRange(roles);
                        await tenantDb.SaveChangesAsync();
                        logger.LogInformation("? Roles created");
                    }

                    // Seed módulos y permisos
                    await TenantModulesSeeder.SeedAsync(tenantDb, logger);

                    // Crear usuario admin y asignar rol
                    if (!await tenantDb.Users.AnyAsync())
                    {
                        var tempPass = Guid.NewGuid().ToString("N").Substring(0, 10);
                        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
                        var hash = hasher.HashPassword(null!, tempPass);
                        
                        var adminUser = new TenantUser
                        {
                            Id = Guid.NewGuid(),
                            Email = $"admin@{slug}",
                            PasswordHash = hash,
                            IsActive = true
                        };
                        
                        tenantDb.Users.Add(adminUser);
                        await tenantDb.SaveChangesAsync();

                        // Asignar rol Admin
                        var adminRole = await tenantDb.Roles.FirstAsync(r => r.Name == "Admin");
                        tenantDb.UserRoles.Add(new TenantUserRole
                        {
                            UserId = adminUser.Id,
                            RoleId = adminRole.Id,
                            AssignedAt = DateTime.UtcNow
                        });
                        await tenantDb.SaveChangesAsync();

                        logger.LogInformation("? Admin user created: {Email}", adminUser.Email);
                        logger.LogWarning("??  TEMP PASSWORD: {Password}", tempPass);
                    }
                }

                // 3. Marcar como Ready
                tenant.EncryptedConnection = protector.Protect(tenantCs);
                tenant.Status = TenantStatus.Ready;
                tenant.LastError = null;
                tenant.UpdatedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();

                logger.LogInformation("?? Tenant {Slug} created successfully", slug);
                
                return Results.Created($"/superadmin/tenants/{slug}", new { slug, status = "Ready" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "? Error creating tenant {Slug}", slug);
                tenant.Status = TenantStatus.Failed;
                tenant.LastError = ex.Message;
                tenant.UpdatedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();
                
                return Results.Problem(statusCode: 500, detail: ex.Message);
            }
        }

        private static async Task<IResult> RepairTenant(
            AdminDbContext adminDb,
            ITenantConnectionProtector protector,
            TenantDbContextFactory factory,
            string tenant)
        {
            var t = await adminDb.Tenants.FirstOrDefaultAsync(x => x.Slug == tenant);
            if (t == null) return Results.NotFound();
            
            try
            {
                var cs = protector.Unprotect(t.EncryptedConnection);
                await using (var tenantDb = factory.Create(cs))
                {
                    await tenantDb.Database.MigrateAsync();
                }
                
                t.Status = TenantStatus.Ready;
                t.LastError = null;
                t.UpdatedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();
                
                return Results.Ok(new { tenant, status = "Ready" });
            }
            catch (Exception ex)
            {
                t.Status = TenantStatus.Failed;
                t.LastError = ex.Message;
                t.UpdatedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();
                
                return Results.Problem(statusCode: 500, detail: ex.Message);
            }
        }
    }
}