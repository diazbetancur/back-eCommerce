using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using CC.Infraestructure.TenantSeeders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.RegularExpressions;

// DTOs para SuperAdmin
public record CreateTenantRequest(string Slug, string Name, string PlanCode, string? AdminEmail = null);
public record ChangeTenantPlanRequest(string PlanCode);

namespace Api_eCommerce.Endpoints
{
    public static class SuperAdminTenants
    {
        public static IEndpointRouteBuilder MapSuperAdminTenants(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/superadmin/tenants")
                .WithTags("SuperAdmin - Tenants");

            group.MapPost("/", CreateTenant)
                .WithName("CreateTenant")
                .WithSummary("Crear nuevo tenant con base de datos y usuario admin");

            group.MapGet("/", ListTenants)
                .WithName("ListTenants")
                .WithSummary("Listar todos los tenants");

            group.MapDelete("/{slug}", DeleteTenant)
                .WithName("SuperAdmin_DisableTenant")
                .WithSummary("Deshabilitar tenant (soft delete)");

            group.MapPatch("/{slug}/plan", ChangeTenantPlan)
                .WithName("ChangeTenantPlan")
                .WithSummary("Cambiar plan de un tenant");

            group.MapPost("/repair", RepairTenant);

            return app;
        }

        // ? NUEVO: Endpoints de planes (solo lectura)
        public static IEndpointRouteBuilder MapSuperAdminPlans(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/superadmin/plans")
                .WithTags("Plans");

            group.MapGet("/", GetPlans)
                .WithName("GetPlans")
                .WithSummary("List available plans (read-only)")
                .Produces<List<PlanDetailDto>>(StatusCodes.Status200OK);

            return app;
        }

        private static async Task<IResult> GetPlans(AdminDbContext adminDb)
        {
            var plans = await adminDb.Plans
                .Include(p => p.Limits)
                .Select(p => new PlanDetailDto
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Limits = p.Limits.Select(l => new PlanLimitDto
                    {
                        LimitCode = l.LimitCode,
                        LimitValue = l.LimitValue,
                        Description = l.Description
                    }).ToList()
                })
                .ToListAsync();

            return Results.Ok(plans);
        }

        private static async Task<IResult> CreateTenant(
            AdminDbContext adminDb,
            ITenantConnectionProtector protector,
            TenantDbContextFactory factory,
            ILoggerFactory loggerFactory,
            [FromBody] CreateTenantRequest request)
        {
            var logger = loggerFactory.CreateLogger("SuperAdminTenants");
            var regex = new Regex("^[a-z0-9-]{3,}$");

            if (!regex.IsMatch(request.Slug))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { { "slug", new[] { "invalid format: must be lowercase letters, numbers and hyphens, min 3 chars" } } });
            }

            // Verificar si ya existe un tenant con ese slug
            var existingTenant = await adminDb.Tenants.FirstOrDefaultAsync(t => t.Slug == request.Slug);
            if (existingTenant != null)
            {
                return Results.Conflict(new { error = $"Tenant with slug '{request.Slug}' already exists", existingStatus = existingTenant.Status.ToString() });
            }

            // Usar adminEmail proporcionado o generar uno por defecto
            var finalAdminEmail = string.IsNullOrWhiteSpace(request.AdminEmail)
                ? $"admin@{request.Slug}"
                : request.AdminEmail.Trim().ToLower();

            var plan = await adminDb.Plans.FirstOrDefaultAsync(p => p.Code == request.PlanCode);
            if (plan == null)
            {
                return Results.NotFound(new { errors = "plan not found" });
            }

            var dbName = $"tenant_{request.Slug}";

            var tenant = new CC.Infraestructure.Admin.Entities.Tenant
            {
                Id = Guid.NewGuid(),
                Slug = request.Slug,
                Name = request.Name,
                DbName = dbName,
                Status = TenantStatus.Pending,
                PlanId = plan.Id,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                adminDb.Tenants.Add(tenant);
                await adminDb.SaveChangesAsync();

                // 1. Crear base de datos - usar defaultdb en lugar de template1 (requerido por Aiven)
                var adminConn = adminDb.Database.GetConnectionString();
                var csb = new NpgsqlConnectionStringBuilder(adminConn);
                var masterCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Database = "defaultdb" }.ToString();

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

                    // Seed m�dulos y permisos
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
                            Email = finalAdminEmail,
                            PasswordHash = hash,
                            IsActive = true,
                            MustChangePassword = true  // Forzar cambio en primer login
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

                        logger.LogInformation("✅ Admin user created: {Email}", adminUser.Email);
                        logger.LogWarning("⚠️  TEMP PASSWORD: {Password}", tempPass);
                    }
                }

                // 3. Marcar como Ready
                tenant.EncryptedConnection = protector.Protect(tenantCs);
                tenant.Status = TenantStatus.Ready;
                tenant.LastError = null;
                tenant.UpdatedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();

                logger.LogInformation("🎉 Tenant {Slug} created successfully", request.Slug);

                return Results.Created($"/superadmin/tenants/{request.Slug}", new { slug = request.Slug, status = "Ready" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error creating tenant {Slug}", request.Slug);
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
            IConfiguration configuration,  // ? AGREGAR para leer template
            string tenant)
        {
            var t = await adminDb.Tenants.FirstOrDefaultAsync(x => x.Slug == tenant);
            if (t == null) return Results.NotFound();

            try
            {
                string cs;

                // ? PLAN B: Si EncryptedConnection es null, reconstruirlo
                if (string.IsNullOrEmpty(t.EncryptedConnection))
                {
                    // Obtener template del appsettings.json
                    var template = configuration["Tenancy:TenantDbTemplate"];
                    if (string.IsNullOrEmpty(template))
                    {
                        return Results.Problem(
                            statusCode: 500,
                            detail: "Tenancy:TenantDbTemplate not configured"
                        );
                    }

                    // Reemplazar {DbName} con el nombre real
                    cs = template.Replace("{DbName}", t.DbName);

                    // Guardar la conexi�n encriptada
                    t.EncryptedConnection = protector.Protect(cs);
                }
                else
                {
                    // Si ya existe, desencriptarla
                    cs = protector.Unprotect(t.EncryptedConnection);
                }

                // Aplicar migraciones a la DB del tenant
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

        // ==================== LIST TENANTS ====================
        private static async Task<IResult> ListTenants(AdminDbContext adminDb)
        {
            var tenants = await adminDb.Tenants
                .Include(t => t.Plan)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TenantListDto
                {
                    Id = t.Id,
                    Slug = t.Slug,
                    Name = t.Name,
                    Status = t.Status.ToString(),
                    PlanCode = t.Plan != null ? t.Plan.Code : null,
                    PlanName = t.Plan != null ? t.Plan.Name : null,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(tenants);
        }

        // ==================== DELETE TENANT ====================
        private static async Task<IResult> DeleteTenant(
            AdminDbContext adminDb,
            string slug)
        {
            var tenant = await adminDb.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);

            if (tenant == null)
            {
                return Results.NotFound(new { error = $"Tenant '{slug}' not found" });
            }

            // Soft delete: cambiar status a Disabled
            tenant.Status = TenantStatus.Disabled;
            tenant.UpdatedAt = DateTime.UtcNow;
            await adminDb.SaveChangesAsync();

            return Results.Ok(new { message = $"Tenant '{slug}' disabled successfully" });
        }

        // ==================== CHANGE TENANT PLAN ====================
        private static async Task<IResult> ChangeTenantPlan(
            AdminDbContext adminDb,
            string slug,
            [FromBody] ChangeTenantPlanRequest request)
        {
            var tenant = await adminDb.Tenants
                .Include(t => t.Plan)
                .FirstOrDefaultAsync(t => t.Slug == slug);

            if (tenant == null)
            {
                return Results.NotFound(new { error = $"Tenant '{slug}' not found" });
            }

            var newPlan = await adminDb.Plans
                .Include(p => p.Limits)
                .FirstOrDefaultAsync(p => p.Code == request.PlanCode);

            if (newPlan == null)
            {
                return Results.NotFound(new { error = $"Plan '{request.PlanCode}' not found" });
            }

            var oldPlanCode = tenant.Plan?.Code ?? "none";
            tenant.PlanId = newPlan.Id;
            tenant.UpdatedAt = DateTime.UtcNow;
            await adminDb.SaveChangesAsync();

            return Results.Ok(new TenantPlanChangedResponse
            {
                TenantSlug = slug,
                OldPlanCode = oldPlanCode,
                NewPlanCode = newPlan.Code,
                NewPlanName = newPlan.Name,
                Limits = newPlan.Limits.Select(l => new PlanLimitDto
                {
                    LimitCode = l.LimitCode,
                    LimitValue = l.LimitValue,
                    Description = l.Description
                }).ToList()
            });
        }
    }

    // ==================== DTOs ====================
    public record PlanDetailDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<PlanLimitDto> Limits { get; set; } = new();
    }

    public record PlanLimitDto
    {
        public string LimitCode { get; set; } = string.Empty;
        public int LimitValue { get; set; }
        public string? Description { get; set; }
    }

    public record TenantListDto
    {
        public Guid Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? PlanCode { get; set; }
        public string? PlanName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public record TenantPlanChangedResponse
    {
        public string TenantSlug { get; set; } = string.Empty;
        public string OldPlanCode { get; set; } = string.Empty;
        public string NewPlanCode { get; set; } = string.Empty;
        public string NewPlanName { get; set; } = string.Empty;
        public List<PlanLimitDto> Limits { get; set; } = new();
    }
}