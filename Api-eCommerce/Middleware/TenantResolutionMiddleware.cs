using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api_eCommerce.Middleware
{
    /// <summary>
    /// Middleware que resuelve el tenant actual basado en el slug y configura el contexto
    /// </summary>
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolutionMiddleware> _logger;

        // Rutas que no requieren resolución de tenant
        private static readonly string[] ExcludedPaths = 
        {
            "/swagger",
            "/health",
            "/provision/tenants/init",
            "/provision/tenants/confirm",
            "/provision/tenants",
            "/superadmin"
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
            // Verificar si la ruta está excluida
            if (IsExcludedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            try
            {
                // Obtener slug del header o query string
                var slug = GetTenantSlug(context);

                if (string.IsNullOrWhiteSpace(slug))
                {
                    _logger.LogWarning("Tenant slug not provided. Path: {Path}", context.Request.Path);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Tenant slug is required",
                        detail = "Provide tenant slug via X-Tenant-Slug header or ?tenant query parameter"
                    });
                    return;
                }

                // Buscar tenant en Admin DB
                var tenant = await adminDb.Tenants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug == slug.ToLower());

                if (tenant == null)
                {
                    _logger.LogWarning("Tenant not found. Slug: {Slug}", slug);
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Tenant not found",
                        detail = $"No tenant found with slug '{slug}'"
                    });
                    return;
                }

                // Verificar que el tenant esté activo
                if (tenant.Status != "Active")
                {
                    _logger.LogWarning("Tenant not active. Slug: {Slug}, Status: {Status}", slug, tenant.Status);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Tenant not available",
                        detail = $"Tenant '{slug}' is in '{tenant.Status}' status",
                        status = tenant.Status
                    });
                    return;
                }

                // Construir connection string desde template
                var connectionString = BuildConnectionString(configuration, tenant.DbName);

                // Crear TenantInfo y guardarlo en el accessor
                var tenantInfo = new TenantInfo
                {
                    Id = tenant.Id,
                    Slug = tenant.Slug,
                    DbName = tenant.DbName,
                    Plan = tenant.Plan,
                    ConnectionString = connectionString
                };

                tenantAccessor.SetTenant(tenantInfo);

                _logger.LogInformation(
                    "Tenant resolved successfully. Slug: {Slug}, DbName: {DbName}, Plan: {Plan}",
                    tenant.Slug, tenant.DbName, tenant.Plan);

                // Continuar con el pipeline
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving tenant");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Internal server error",
                    detail = "An error occurred while resolving tenant"
                });
            }
        }

        private static bool IsExcludedPath(PathString path)
        {
            var pathValue = path.Value?.ToLower() ?? string.Empty;
            return ExcludedPaths.Any(excluded => pathValue.StartsWith(excluded));
        }

        private static string GetTenantSlug(HttpContext context)
        {
            // Prioridad: Header > Query String
            var slug = context.Request.Headers["X-Tenant-Slug"].FirstOrDefault();
            
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = context.Request.Query["tenant"].FirstOrDefault();
            }

            return slug?.Trim() ?? string.Empty;
        }

        private static string BuildConnectionString(IConfiguration configuration, string dbName)
        {
            var template = configuration["Tenancy:TenantDbTemplate"];
            
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new InvalidOperationException("Tenancy:TenantDbTemplate configuration is missing");
            }

            // Reemplazar {DbName} en el template
            return template.Replace("{DbName}", dbName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
