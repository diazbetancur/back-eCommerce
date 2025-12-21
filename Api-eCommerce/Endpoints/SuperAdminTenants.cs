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

            group.MapGet("/", ListTenants);
            group.MapPost("/", CreateTenant);
            group.MapPost("/repair", RepairTenant);
            group.MapDelete("/{slug}", DeleteTenant);

            // ? Plan management
            group.MapPatch("/{slug}/plan", ChangeTenantPlan)
                .WithName("ChangeTenantPlan")
                .WithSummary("Change tenant plan (upgrade/downgrade)")
                .Produces<TenantPlanChangedResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

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
            string slug,
            string name,
            string planCode,
            string? adminEmail = null)  // ✅ Email opcional del admin
        {
            var logger = loggerFactory.CreateLogger("SuperAdminTenants");
            var regex = new Regex("^[a-z0-9-]{3,}$");

            if (!regex.IsMatch(slug))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { { "slug", new[] { "invalid format" } } });
            }

            // Validar email si se proporciona
            if (!string.IsNullOrEmpty(adminEmail) && !IsValidEmail(adminEmail))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { { "adminEmail", new[] { "invalid email format" } } });
            }

            var plan = await adminDb.Plans.FirstOrDefaultAsync(p => p.Code == planCode);
            if (plan == null)
            {
                return Results.NotFound(new { errors = "plan not found" });
            }

            var dbName = $"tenant_{slug}";

            var tenant = new CC.Infraestructure.Admin.Entities.Tenant
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                Name = name,
                DbName = dbName,
                Status = TenantStatus.Pending,
                PlanId = plan.Id,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                adminDb.Tenants.Add(tenant);
                await adminDb.SaveChangesAsync();

                // 1. Construir connection string para el tenant
                var adminConn = adminDb.Database.GetConnectionString()!;

                // Connection string para la nueva DB del tenant
                var tenantCs = new NpgsqlConnectionStringBuilder(adminConn) { Database = dbName }.ToString();

                // Verificar si la DB existe usando la conexión de AdminDb (ecommerce_admin)
                bool dbExists = await CheckDatabaseExistsAsync(adminConn, dbName, logger);

                if (!dbExists)
                {
                    // Intentar crear la base de datos usando la conexión de AdminDb
                    try
                    {
                        await CreateDatabaseAsync(adminConn, dbName, logger);
                    }
                    catch (PostgresException pgEx) when (pgEx.SqlState == "28000" || pgEx.SqlState == "42501" || pgEx.SqlState == "42000")
                    {
                        // Aiven/Cloud no permite crear DBs - usar la misma DB con esquema
                        logger.LogWarning("⚠️ Cannot create database in cloud PostgreSQL. Using schema-based isolation.");
                        tenantCs = adminConn; // Usar la misma DB que AdminDb
                        tenant.DbName = $"schema_{slug}"; // Indicar que usa esquema
                    }
                }

                tenant.Status = TenantStatus.Seeding;
                await adminDb.SaveChangesAsync();

                // Variables para las credenciales del admin
                string? createdAdminEmail = null;
                string? createdAdminPassword = null;

                // 2. Aplicar migraciones y seed
                await using (var tenantDb = factory.Create(tenantCs))
                {
                    // Aplicar migraciones
                    await tenantDb.Database.MigrateAsync();
                    logger.LogInformation("✅ Migrations applied");

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
                        logger.LogInformation("✅ Roles created");
                    }

                    // Seed módulos y permisos
                    await TenantModulesSeeder.SeedAsync(tenantDb, logger);

                    // Crear usuario admin y asignar rol
                    if (!await tenantDb.Users.AnyAsync())
                    {
                        // Generar contraseña temporal segura (12 caracteres)
                        var tempPass = GenerateSecurePassword();
                        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
                        var hash = hasher.HashPassword(null!, tempPass);

                        // Usar el email proporcionado o generar uno por defecto
                        var finalAdminEmail = !string.IsNullOrEmpty(adminEmail)
                            ? adminEmail
                            : $"admin@{slug}.local";

                        var adminUser = new TenantUser
                        {
                            Id = Guid.NewGuid(),
                            Email = finalAdminEmail,
                            PasswordHash = hash,
                            IsActive = true,
                            MustChangePassword = true  // ✅ Forzar cambio de contraseña
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

                        // Guardar credenciales para la respuesta
                        createdAdminEmail = finalAdminEmail;
                        createdAdminPassword = tempPass;

                        logger.LogInformation("✅ Admin user created: {Email}", finalAdminEmail);
                        logger.LogWarning("⚠️ TEMP PASSWORD generated (must change on first login)");
                    }
                }

                // 3. Marcar como Ready
                tenant.EncryptedConnection = protector.Protect(tenantCs);
                tenant.Status = TenantStatus.Ready;
                tenant.LastError = null;
                tenant.UpdatedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();

                logger.LogInformation("🎉 Tenant {Slug} created successfully", slug);

                // ✅ Devolver respuesta con credenciales del admin
                return Results.Created($"/superadmin/tenants/{slug}", new TenantCreatedResponse
                {
                    Slug = slug,
                    Name = name,
                    Status = "Ready",
                    Plan = planCode,
                    AdminCredentials = createdAdminEmail != null ? new AdminCredentialsDto
                    {
                        Email = createdAdminEmail,
                        TemporaryPassword = createdAdminPassword!,
                        MustChangePassword = true,
                        Message = "⚠️ Esta contraseña es temporal. El usuario debe cambiarla en el primer inicio de sesión."
                    } : null
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error creating tenant {Slug}", slug);
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

        /// <summary>
        /// Lista todos los tenants registrados
        /// </summary>
        private static async Task<IResult> ListTenants(AdminDbContext adminDb)
        {
            var tenants = await adminDb.Tenants
                .Include(t => t.Plan)
                .Select(t => new
                {
                    t.Id,
                    t.Slug,
                    t.Name,
                    t.DbName,
                    Status = t.Status.ToString(),
                    Plan = t.Plan != null ? t.Plan.Code : null,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(tenants);
        }

        /// <summary>
        /// Elimina un tenant y su base de datos
        /// </summary>
        private static async Task<IResult> DeleteTenant(
            AdminDbContext adminDb,
            ILoggerFactory loggerFactory,
            string slug,
            bool dropDatabase = true)
        {
            var logger = loggerFactory.CreateLogger("SuperAdminTenants");

            var tenant = await adminDb.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
            if (tenant == null)
            {
                return Results.NotFound(new { error = $"Tenant '{slug}' not found" });
            }

            try
            {
                var dbName = tenant.DbName;
                var adminConn = adminDb.Database.GetConnectionString()!;

                // 1. Eliminar el registro del tenant primero
                adminDb.Tenants.Remove(tenant);
                await adminDb.SaveChangesAsync();
                logger.LogInformation("✅ Tenant record deleted: {Slug}", slug);

                // 2. Si se solicita, eliminar la base de datos
                if (dropDatabase && !dbName.StartsWith("schema_"))
                {
                    try
                    {
                        await DropDatabaseAsync(adminConn, dbName, logger);
                        logger.LogInformation("✅ Database dropped: {DbName}", dbName);
                    }
                    catch (PostgresException pgEx) when (pgEx.SqlState == "3D000")
                    {
                        // La base de datos no existe, ignorar
                        logger.LogWarning("⚠️ Database {DbName} does not exist, skipping drop", dbName);
                    }
                    catch (PostgresException pgEx)
                    {
                        // Otro error de PostgreSQL, loguear pero continuar
                        logger.LogWarning("⚠️ Could not drop database {DbName}: {Error}", dbName, pgEx.Message);
                    }
                }

                return Results.Ok(new
                {
                    message = $"Tenant '{slug}' deleted successfully",
                    databaseDropped = dropDatabase && !dbName.StartsWith("schema_")
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error deleting tenant {Slug}", slug);
                return Results.Problem(statusCode: 500, detail: ex.Message);
            }
        }

        /// <summary>
        /// Elimina una base de datos en PostgreSQL
        /// </summary>
        private static async Task DropDatabaseAsync(string connectionString, string dbName, ILogger logger)
        {
            logger.LogInformation("🔧 Attempting to drop database: {DbName}", dbName);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            // Primero terminar todas las conexiones activas a esa base de datos
            var terminateSql = $@"
                SELECT pg_terminate_backend(pg_stat_activity.pid)
                FROM pg_stat_activity
                WHERE pg_stat_activity.datname = '{dbName}'
                AND pid <> pg_backend_pid();";

            await using (var terminateCmd = new NpgsqlCommand(terminateSql, conn))
            {
                await terminateCmd.ExecuteNonQueryAsync();
            }

            logger.LogInformation("✅ Terminated active connections to {DbName}", dbName);

            // Luego eliminar la base de datos
            var dropSql = $"DROP DATABASE IF EXISTS \"{dbName}\";";
            logger.LogInformation("🔧 Executing: {Sql}", dropSql);

            await using var cmd = new NpgsqlCommand(dropSql, conn);
            await cmd.ExecuteNonQueryAsync();

            logger.LogInformation("✅ Dropped database: {DbName}", dbName);
        }

        /// <summary>
        /// Verifica si una base de datos existe en PostgreSQL
        /// </summary>
        private static async Task<bool> CheckDatabaseExistsAsync(string connectionString, string dbName, ILogger logger)
        {
            try
            {
                // Usar la conexión actual (AdminDb) para verificar - NO cambiar la base de datos
                // En Aiven/Cloud, no existe "postgres" como DB por defecto
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                await using var cmd = new NpgsqlCommand(
                    "SELECT 1 FROM pg_database WHERE datname = @dbname", conn);
                cmd.Parameters.AddWithValue("dbname", dbName);

                var result = await cmd.ExecuteScalarAsync();
                return result != null;
            }
            catch (PostgresException ex)
            {
                logger.LogWarning("⚠️ Cannot check database existence: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Crea una base de datos en PostgreSQL usando la conexión de AdminDb
        /// En Aiven/Cloud se debe conectar a la DB existente (no a "postgres")
        /// </summary>
        private static async Task CreateDatabaseAsync(string connectionString, string dbName, ILogger logger)
        {
            logger.LogInformation("🔧 Attempting to create database: {DbName}", dbName);
            logger.LogInformation("🔧 Using connection to existing DB (AdminDb) to create new DB");

            // Usar la conexión actual (AdminDb) - NO cambiar a "postgres"
            // En Aiven, podemos crear DBs desde cualquier base de datos existente
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            logger.LogInformation("✅ Connected to AdminDb successfully");

            // Usar comillas dobles para el nombre de la DB
            var sql = $"CREATE DATABASE \"{dbName}\" WITH ENCODING 'UTF8';";
            logger.LogInformation("🔧 Executing: {Sql}", sql);

            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();

            logger.LogInformation("✅ Created database: {DbName}", dbName);
        }

        /// <summary>
        /// Genera una contraseña segura temporal
        /// </summary>
        private static string GenerateSecurePassword()
        {
            const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string special = "!@#$%&*";

            var random = new Random();
            var password = new char[12];

            // Asegurar al menos uno de cada tipo
            password[0] = upperCase[random.Next(upperCase.Length)];
            password[1] = lowerCase[random.Next(lowerCase.Length)];
            password[2] = digits[random.Next(digits.Length)];
            password[3] = special[random.Next(special.Length)];

            // Llenar el resto con caracteres aleatorios
            var allChars = upperCase + lowerCase + digits + special;
            for (int i = 4; i < 12; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }

            // Mezclar el array
            return new string(password.OrderBy(x => random.Next()).ToArray());
        }

        /// <summary>
        /// Valida formato de email
        /// </summary>
        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cambiar el plan de un tenant (upgrade/downgrade)
        /// Solo SuperAdmin puede ejecutar esta operación
        /// </summary>
        private static async Task<IResult> ChangeTenantPlan(
            AdminDbContext adminDb,
            ILoggerFactory loggerFactory,
            string slug,
            string newPlanCode)
        {
            var logger = loggerFactory.CreateLogger("SuperAdminTenants");

            // Buscar el tenant
            var tenant = await adminDb.Tenants
                .Include(t => t.Plan)
                    .ThenInclude(p => p!.Limits)
                .FirstOrDefaultAsync(t => t.Slug == slug);

            if (tenant == null)
            {
                return Results.NotFound(new { error = $"Tenant '{slug}' not found" });
            }

            // Buscar el nuevo plan
            var newPlan = await adminDb.Plans
                .Include(p => p.Limits)
                .FirstOrDefaultAsync(p => p.Code == newPlanCode);

            if (newPlan == null)
            {
                return Results.NotFound(new { error = $"Plan '{newPlanCode}' not found" });
            }

            // Verificar si ya tiene el mismo plan
            if (tenant.PlanId == newPlan.Id)
            {
                return Results.BadRequest(new { error = $"Tenant already has plan '{newPlanCode}'" });
            }

            var previousPlan = tenant.Plan;
            var previousPlanCode = previousPlan?.Code ?? "None";

            // Actualizar el plan
            tenant.PlanId = newPlan.Id;
            tenant.UpdatedAt = DateTime.UtcNow;
            await adminDb.SaveChangesAsync();

            logger.LogInformation(
                "📋 Tenant {Slug} plan changed: {OldPlan} → {NewPlan}",
                slug, previousPlanCode, newPlanCode);

            // Construir respuesta con los nuevos límites
            var response = new TenantPlanChangedResponse
            {
                Slug = slug,
                PreviousPlan = previousPlanCode,
                NewPlan = newPlanCode,
                ChangedAt = DateTime.UtcNow,
                NewLimits = newPlan.Limits.Select(l => new PlanLimitDto
                {
                    LimitCode = l.LimitCode,
                    LimitValue = l.LimitValue,
                    Description = l.Description
                }).ToList(),
                Message = previousPlanCode == "Premium" && newPlanCode == "Basic"
                    ? "⚠️ Downgrade aplicado. Si excede los nuevos límites, las operaciones de creación/edición estarán bloqueadas hasta depurar."
                    : "✅ Plan actualizado exitosamente."
            };

            return Results.Ok(response);
        }
    }

    // ==================== DTOs ====================

    /// <summary>
    /// Respuesta al crear un tenant exitosamente
    /// </summary>
    public record TenantCreatedResponse
    {
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public AdminCredentialsDto? AdminCredentials { get; set; }
    }

    /// <summary>
    /// Credenciales del usuario admin creado
    /// </summary>
    public record AdminCredentialsDto
    {
        public string Email { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; } = true;
        public string Message { get; set; } = string.Empty;
    }

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

    /// <summary>
    /// Respuesta al cambiar el plan de un tenant
    /// </summary>
    public record TenantPlanChangedResponse
    {
        public string Slug { get; set; } = string.Empty;
        public string PreviousPlan { get; set; } = string.Empty;
        public string NewPlan { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public List<PlanLimitDto> NewLimits { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}