using Api_eCommerce.Authorization;
using CC.Aplication.Admin;
using CC.Infraestructure.Admin.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Api_eCommerce.Endpoints
{
    /// <summary>
    /// Endpoints administrativos - NO requieren X-Tenant-Slug
    /// Solo usan AdminDbContext
    /// Requieren roles administrativos específicos (SuperAdmin, TenantManager, Support, Viewer)
    /// </summary>
    public static class AdminEndpoints
    {
        public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
        {
            var admin = app.MapGroup("/admin")
                .WithTags("Admin Panel")
                .AddEndpointFilter<AdminRoleAuthorizationFilter>();

            // ==================== AUTH ====================
            var auth = admin.MapGroup("/auth");

            auth.MapPost("/login", AdminLogin)
                .WithName("AdminLogin")
                .WithSummary("Admin login")
                .WithDescription("Authenticates an admin user and returns JWT token. Does NOT require X-Tenant-Slug header.")
                .AllowAnonymous()
                .Produces<AdminLoginResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            auth.MapGet("/me", GetAdminProfile)
                .WithName("GetAdminProfile")
                .WithSummary("Get current admin user profile")
                .WithDescription("Returns the authenticated admin user's profile. Does NOT require X-Tenant-Slug header.")
                .RequireAuthorization()
                .Produces<AdminUserDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            // ==================== TENANTS ====================
            var tenants = admin.MapGroup("/tenants")
                .RequireAuthorization();

            tenants.MapGet("/", GetTenants)
                .WithName("GetTenants")
                .WithSummary("List all tenants (paginated)")
                .Produces<PagedTenantsResponse>(StatusCodes.Status200OK);

            tenants.MapGet("/{tenantId:guid}", GetTenantById)
                .WithName("GetTenantById")
                .WithSummary("Get tenant details by ID")
                .Produces<TenantDetailDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            tenants.MapPatch("/{tenantId:guid}", UpdateTenant)
                .WithName("UpdateTenant")
                .WithSummary("Update tenant configuration")
                .WithMetadata(new RequireAdminRoleAttribute(AdminRoleNames.SuperAdmin, AdminRoleNames.TenantManager))
                .Produces<TenantDetailDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            tenants.MapPatch("/{tenantId:guid}/status", UpdateTenantStatus)
                .WithName("UpdateTenantStatus")
                .WithSummary("Update tenant status")
                .WithMetadata(new RequireAdminRoleAttribute(AdminRoleNames.SuperAdmin, AdminRoleNames.TenantManager))
                .Produces<TenantDetailDto>(StatusCodes.Status200OK);

            tenants.MapDelete("/{tenantId:guid}", DeleteTenant)
                .WithName("Admin_DeleteTenant")
                .WithSummary("Delete tenant (dangerous operation) - SuperAdmin only")
                .WithMetadata(new RequireAdminRoleAttribute(AdminRoleNames.SuperAdmin))
                .Produces(StatusCodes.Status204NoContent);

            return app;
        }

        // ==================== HANDLERS ====================

        private static async Task<IResult> AdminLogin(
            [FromBody] AdminLoginRequest request,
            IAdminAuthService authService,
            IAdminAuditService auditService,
            HttpContext httpContext)
        {
            try
            {
                var result = await authService.LoginAsync(request);

                // Registrar auditoría (login exitoso)
                await auditService.LogActionAsync(
                    result.User.Id,
                    result.User.Email,
                    AuditActions.LoginSuccess,
                    AuditResourceTypes.Authentication,
                    result.User.Id.ToString(),
                    null,
                    AdminAuditEndpoints.GetIpAddress(httpContext),
                    AdminAuditEndpoints.GetUserAgent(httpContext)
                );

                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Registrar auditoría (login fallido) - solo si tenemos el email
                if (!string.IsNullOrEmpty(request.Email))
                {
                    await auditService.LogActionAsync(
                        Guid.Empty, // No hay usuario válido
                        request.Email,
                        AuditActions.LoginFailed,
                        AuditResourceTypes.Authentication,
                        null,
                        new { Reason = ex.Message },
                        AdminAuditEndpoints.GetIpAddress(httpContext),
                        AdminAuditEndpoints.GetUserAgent(httpContext)
                    );
                }

                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred during login"
                );
            }
        }

        private static async Task<IResult> GetAdminProfile(
            HttpContext context,
            IAdminAuthService authService)
        {
            try
            {
                var userId = GetAdminUserIdFromJwt(context);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Unauthorized",
                        detail: "Invalid or missing admin credentials in token"
                    );
                }

                var user = await authService.GetCurrentUserAsync(userId.Value);
                return Results.Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving admin profile"
                );
            }
        }

        private static async Task<IResult> GetTenants(
            [AsParameters] TenantListQuery query,
            IAdminTenantService tenantService)
        {
            try
            {
                var result = await tenantService.GetTenantsAsync(query);
                return Results.Ok(result);
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving tenants"
                );
            }
        }

        private static async Task<IResult> GetTenantById(
            Guid tenantId,
            IAdminTenantService tenantService)
        {
            try
            {
                var tenant = await tenantService.GetTenantByIdAsync(tenantId);
                return Results.Ok(tenant);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: ex.Message
                );
            }
        }

        private static async Task<IResult> UpdateTenant(
            Guid tenantId,
            [FromBody] UpdateTenantRequest request,
            IAdminTenantService tenantService)
        {
            try
            {
                var tenant = await tenantService.UpdateTenantAsync(tenantId, request);
                return Results.Ok(tenant);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: ex.Message
                );
            }
        }

        private static async Task<IResult> UpdateTenantStatus(
            Guid tenantId,
            [FromBody] UpdateTenantStatusRequest request,
            IAdminTenantService tenantService)
        {
            try
            {
                var tenant = await tenantService.UpdateTenantStatusAsync(tenantId, request);
                return Results.Ok(tenant);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Bad Request",
                    detail: ex.Message
                );
            }
        }

        private static async Task<IResult> DeleteTenant(
            Guid tenantId,
            IAdminTenantService tenantService)
        {
            try
            {
                await tenantService.DeleteTenantAsync(tenantId);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Not Found",
                    detail: ex.Message
                );
            }
        }

        // ==================== HELPERS ====================

        private static Guid? GetAdminUserIdFromJwt(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                // Verificar que es un admin (claim especial)
                var isAdmin = context.User.FindFirst("admin")?.Value == "true";
                if (isAdmin)
                {
                    return userId;
                }
            }

            return null;
        }
    }
}
