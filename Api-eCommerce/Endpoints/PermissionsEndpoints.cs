using CC.Aplication.Permissions;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Endpoints
{
    public static class PermissionsEndpoints
    {
        public static IEndpointRouteBuilder MapPermissionsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/me")
                .RequireAuthorization()
                .WithTags("User Permissions");

            group.MapGet("/modules", GetUserModules)
                .WithName("GetUserModules")
                .WithSummary("Get user's available modules and permissions")
                .WithDescription("Returns list of modules the user can access with their specific permissions")
                .Produces<ModulesResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            group.MapGet("/modules/{moduleCode}/permissions", GetModulePermissions)
                .WithName("GetModulePermissions")
                .WithSummary("Get user's permissions for a specific module")
                .WithDescription("Returns the user's permissions (view, create, update, delete) for a module")
                .Produces<ModulePermissions>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            return app;
        }

        private static async Task<IResult> GetUserModules(
            HttpContext context,
            IPermissionService permissionService,
            ITenantResolver tenantResolver)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Obtener user ID del token JWT
                var userId = GetUserIdFromJwt(context);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Obtener m�dulos del usuario
                var modules = await permissionService.GetUserModulesAsync(userId.Value);

                return Results.Ok(new ModulesResponse
                {
                    Modules = modules.Select(m => new ModuleResponse
                    {
                        Code = m.Code,
                        Name = m.Name,
                        Description = m.Description,
                        IconName = m.IconName,
                        Permissions = new PermissionsResponse
                        {
                            CanView = m.Permissions.CanView,
                            CanCreate = m.Permissions.CanCreate,
                            CanUpdate = m.Permissions.CanUpdate,
                            CanDelete = m.Permissions.CanDelete
                        }
                    }).ToList()
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving modules"
                );
            }
        }

        private static async Task<IResult> GetModulePermissions(
            HttpContext context,
            string moduleCode,
            IPermissionService permissionService,
            ITenantResolver tenantResolver)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Obtener user ID del token JWT
                var userId = GetUserIdFromJwt(context);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Obtener permisos del m�dulo
                var permissions = await permissionService.GetUserPermissionsAsync(userId.Value, moduleCode);

                return Results.Ok(permissions);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving permissions"
                );
            }
        }

        private static Guid? GetUserIdFromJwt(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirst("sub");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
    }

    // ==================== DTOs ====================

    public record ModulesResponse
    {
        public List<ModuleResponse> Modules { get; set; } = new();
    }

    public record ModuleResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? IconName { get; set; }
        public PermissionsResponse Permissions { get; set; } = new();
    }

    public record PermissionsResponse
    {
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }
    }
}