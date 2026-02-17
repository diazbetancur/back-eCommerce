using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api_eCommerce.Middleware
{
    /// <summary>
    /// Middleware que resuelve el tenant actual basado en el slug y configura el contexto
    /// ?? IMPORTANTE: Este middleware SOLO debe ejecutarse en rutas tenant-scoped
    /// NO debe ejecutarse en rutas administrativas (/admin, /provision, /superadmin, /health)
    /// </summary>
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolutionMiddleware> _logger;

        private static readonly string[] ExcludedPaths =
        {
            "/swagger",
            "/health",
            "/admin/auth",               // ? Admin login (usa AdminDb, NO requiere tenant)
            "/admin/tenants",            // ? Gestión de tenants por SuperAdmin (usa AdminDb)
            "/provision",                // ? Provisioning (usa AdminDb)
            "/superadmin",               // ? SuperAdmin endpoints (usa AdminDb)
            "/_framework",               // ? Blazor/framework routes
            "/_vs"                       // ? Visual Studio routes
        };

        public TenantResolutionMiddleware(
            RequestDelegate next,
            ILogger<TenantResolutionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            AdminDbContext adminDb,
            ITenantAccessor tenantAccessor,
            IConfiguration configuration)
        {
            // ==================== IGNORAR PETICIONES OPTIONS (CORS Preflight) ====================
            // Las peticiones OPTIONS deben ser manejadas por el middleware CORS
            if (context.Request.Method == "OPTIONS")
            {
                await _next(context);
                return;
            }

            // ==================== VERIFICAR SI LA RUTA ESTÁ EXCLUIDA ====================
            if (IsExcludedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            try
            {
                // ==================== OBTENER SLUG ====================
                var slug = GetTenantSlug(context);

                if (string.IsNullOrWhiteSpace(slug))
                {
                    _logger.LogWarning("Tenant slug not provided. Path: {Path}", context.Request.Path);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Tenant Required",
                        detail = "This endpoint requires a tenant. Provide tenant slug via X-Tenant-Slug header or ?tenant query parameter",
                        path = context.Request.Path.Value
                    });
                    return;
                }

                // ==================== BUSCAR TENANT EN ADMIN DB ====================
                var tenant = await adminDb.Tenants
                    .Include(t => t.Plan) // ? Incluir plan para tener info completa
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug == slug.ToLower());

                if (tenant == null)
                {
                    _logger.LogWarning("Tenant not found. Slug: {Slug}", slug);
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Tenant Not Found",
                        detail = $"No tenant found with slug '{slug}'",
                        slug
                    });
                    return;
                }

                // ==================== VERIFICAR STATUS ====================
                // Solo permitir tenants en status "Ready"
                if (tenant.Status != TenantStatus.Ready)
                {
                    _logger.LogWarning(
                        "Tenant not available. Slug: {Slug}, Status: {Status}",
                        slug, tenant.Status);

                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Tenant Not Available",
                        detail = $"Tenant '{slug}' is currently unavailable",
                        status = tenant.Status.ToString(),
                        slug
                    });
                    return;
                }

                // ==================== CONSTRUIR CONNECTION STRING ====================
                var connectionString = BuildConnectionString(configuration, tenant.DbName);

                // ==================== CREAR TENANT INFO ====================
                var tenantInfo = new TenantInfo
                {
                    Id = tenant.Id,
                    Slug = tenant.Slug,
                    DbName = tenant.DbName,
                    Plan = tenant.Plan?.Name ?? "Unknown",
                    ConnectionString = connectionString
                };

                // Guardar en el accessor para que esté disponible en todo el request
                tenantAccessor.SetTenant(tenantInfo);

                _logger.LogDebug("Tenant resolved: {Slug}", tenant.Slug);

                // ==================== CONTINUAR CON EL PIPELINE ====================
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving tenant for path: {Path}", context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal Server Error",
                    detail = "An error occurred while resolving tenant",
                    path = context.Request.Path.Value
                });
            }
        }

        // ==================== PRIVATE HELPERS ====================

        private static bool IsExcludedPath(PathString path)
        {
            var pathValue = path.Value?.ToLower() ?? string.Empty;
            return ExcludedPaths.Any(excluded => pathValue.StartsWith(excluded, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetTenantSlug(HttpContext context)
        {
            // Prioridad: Header > Query String > JWT
            var slug = context.Request.Headers["X-Tenant-Slug"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = context.Request.Query["tenant"].FirstOrDefault();
            }

            // Si no se encuentra en header ni query, intentar leer desde JWT
            if (string.IsNullOrWhiteSpace(slug))
            {
                var tenantSlugClaim = context.User?.FindFirst("tenant_slug");
                slug = tenantSlugClaim?.Value;
            }

            return slug?.Trim() ?? string.Empty;
        }

        private static string BuildConnectionString(IConfiguration configuration, string dbName)
        {
            var template = configuration["Tenancy:TenantDbTemplate"];

            if (string.IsNullOrWhiteSpace(template))
            {
                throw new InvalidOperationException(
                    "Tenancy:TenantDbTemplate configuration is missing. " +
                    "Example: 'Host=localhost;Database={DbName};Username=postgres;Password=...'");
            }

            // Reemplazar {DbName} en el template
            return template.Replace("{DbName}", dbName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
