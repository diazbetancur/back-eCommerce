using CC.Aplication.Admin;
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
    /// </summary>
    public static class AdminEndpoints
    {
        public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
        {
            var admin = app.MapGroup("/admin")
                .WithTags("Admin Panel");

            // ==================== AUTH ====================
            var auth = admin.MapGroup("/auth");

            auth.MapPost("/login", AdminLogin)
                .WithName("AdminLogin")
                .WithSummary("Admin login")
                .AllowAnonymous()
                .Produces<AdminLoginResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            auth.MapGet("/me", GetAdminProfile)
                .WithName("GetAdminProfile")
                .WithSummary("Get current admin user profile")
                .RequireAuthorization()
                .Produces<AdminUserDto>(StatusCodes.Status200OK);

            // ==================== TENANTS ====================
            var tenants = admin.MapGroup("/tenants")
                .RequireAuthorization();

            tenants.MapGet("", GetTenants)
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
                .Produces<TenantDetailDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            tenants.MapPatch("/{tenantId:guid}/status", UpdateTenantStatus)
                .WithName("UpdateTenantStatus")
                .WithSummary("Update tenant status")
                .Produces<TenantDetailDto>(StatusCodes.Status200OK);

            tenants.MapDelete("/{tenantId:guid}", DeleteTenant)
                .WithName("DeleteTenant")
                .WithSummary("Delete tenant (dangerous operation)")
                .Produces(StatusCodes.Status204NoContent);

            return app;
        }

        // ==================== HANDLERS ====================

        private static async Task<IResult> AdminLogin(
            [FromBody] AdminLoginRequest request,
            IAdminAuthService authService)
        {
            try
            {
                var result = await authService.LoginAsync(request);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
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
                    return Results.Unauthorized();
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
            catch (Exception ex)
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
