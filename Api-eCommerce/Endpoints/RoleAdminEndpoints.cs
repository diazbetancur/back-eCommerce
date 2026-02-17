using Api_eCommerce.Authorization;
using CC.Aplication.Roles;
using CC.Domain.Dto;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Api_eCommerce.Endpoints
{
  /// <summary>
  /// Endpoints de administración de roles - Requieren X-Tenant-Slug y autorización
  /// Acceso basado en permisos del módulo "users"
  /// </summary>
  public static class RoleAdminEndpoints
  {
    public static IEndpointRouteBuilder MapRoleAdminEndpoints(this IEndpointRouteBuilder app)
    {
      var roles = app.MapGroup("/admin/roles")
          .RequireAuthorization()
          .AddEndpointFilter<ModuleAuthorizationFilter>()
          .WithTags("Role Management");

      // ==================== ROLES ====================
      roles.MapGet("/", GetRoles)
          .WithName("GetRoles")
          .WithSummary("Get all roles in the tenant")
          .WithDescription("Returns a list of all roles with their assigned user count")
          .WithMetadata(new RequireModuleAttribute("users", "view"))
          .Produces<RolesResponse>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

      roles.MapGet("/{roleId:guid}", GetRoleById)
          .WithName("GetRoleById")
          .WithSummary("Get role details by ID")
          .WithDescription("Returns detailed information about a role including users and permissions")
          .WithMetadata(new RequireModuleAttribute("users", "view"))
          .Produces<RoleDetailDto>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      roles.MapPost("/", CreateRole)
          .WithName("CreateRole")
          .WithSummary("Create a new role")
          .WithDescription("Creates a new role with the specified name and description")
          .WithMetadata(new RequireModuleAttribute("users", "create"))
          .Produces<RoleDetailDto>(StatusCodes.Status201Created)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
          .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

      roles.MapPut("/{roleId:guid}", UpdateRole)
          .WithName("UpdateRole")
          .WithSummary("Update role information")
          .WithDescription("Updates role name and/or description. System roles cannot be renamed.")
          .WithMetadata(new RequireModuleAttribute("users", "update"))
          .Produces<RoleDetailDto>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
          .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

      roles.MapDelete("/{roleId:guid}", DeleteRole)
          .WithName("DeleteRole")
          .WithSummary("Delete a role")
          .WithDescription("Deletes a role if it has no assigned users. System roles (SuperAdmin, Customer) cannot be deleted.")
          .WithMetadata(new RequireModuleAttribute("users", "delete"))
          .Produces(StatusCodes.Status204NoContent)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
          .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

      // ==================== ROLE PERMISSIONS ====================
      roles.MapGet("/available-modules", GetAvailableModules)
          .WithName("GetAvailableModules")
          .WithSummary("Get available modules and permissions")
          .WithDescription("Returns all modules with their available permissions for role assignment")
          .WithMetadata(new RequireModuleAttribute("users", "view"))
          .Produces<AvailableModulesResponse>(StatusCodes.Status200OK);

      roles.MapGet("/{roleId:guid}/permissions", GetRolePermissions)
          .WithName("GetRolePermissions")
          .WithSummary("Get role permissions")
          .WithDescription("Returns all module permissions assigned to a specific role")
          .WithMetadata(new RequireModuleAttribute("users", "view"))
          .Produces<RolePermissionsResponse>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      roles.MapPut("/{roleId:guid}/permissions", UpdateRolePermissions)
          .WithName("UpdateRolePermissions")
          .WithSummary("Update role permissions")
          .WithDescription("Updates the permissions assigned to a role for specific modules")
          .WithMetadata(new RequireModuleAttribute("users", "update"))
          .Produces<RolePermissionsResponse>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      return app;
    }

    // ==================== HANDLER METHODS ====================

    private static async Task<IResult> GetRoles(
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      var response = await roleService.GetRolesAsync(cancellationToken);
      return Results.Ok(response);
    }

    private static async Task<IResult> GetRoleById(
        Guid roleId,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      var role = await roleService.GetRoleByIdAsync(roleId, cancellationToken);

      if (role == null)
      {
        return Results.Problem(
            title: "Role not found",
            detail: $"No role found with ID {roleId}",
            statusCode: StatusCodes.Status404NotFound
        );
      }

      return Results.Ok(role);
    }

    private static async Task<IResult> CreateRole(
        [FromBody] CreateRoleRequest request,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      // Validación básica
      if (string.IsNullOrWhiteSpace(request.Name))
      {
        return Results.Problem(
            title: "Validation failed",
            detail: "Role name is required",
            statusCode: StatusCodes.Status400BadRequest
        );
      }

      var role = await roleService.CreateRoleAsync(request, cancellationToken);

      if (role == null)
      {
        return Results.Problem(
            title: "Role creation failed",
            detail: $"A role with the name '{request.Name}' already exists",
            statusCode: StatusCodes.Status409Conflict
        );
      }

      return Results.Created($"/admin/roles/{role.Id}", role);
    }

    private static async Task<IResult> UpdateRole(
        Guid roleId,
        [FromBody] UpdateRoleRequest request,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      // Obtener el rol para verificar si es del sistema
      var existingRole = await roleService.GetRoleByIdAsync(roleId, cancellationToken);
      if (existingRole == null)
      {
        return Results.Problem(
            title: "Role not found",
            detail: $"Role with ID {roleId} not found",
            statusCode: StatusCodes.Status404NotFound
        );
      }

      // Validación: No permitir cambios a roles del sistema
      var isSystemRole = await roleService.IsSystemRoleAsync(existingRole.Name, cancellationToken);
      if (isSystemRole)
      {
        return Results.Problem(
            title: "Cannot modify system role",
            detail: "System roles (SuperAdmin, Customer) cannot be modified",
            statusCode: StatusCodes.Status400BadRequest
        );
      }

      var role = await roleService.UpdateRoleAsync(roleId, request, cancellationToken);

      if (role == null)
      {
        return Results.Problem(
            title: "Update failed",
            detail: $"Role with ID {roleId} not found or name already exists",
            statusCode: StatusCodes.Status404NotFound
        );
      }

      return Results.Ok(role);
    }

    private static async Task<IResult> DeleteRole(
        Guid roleId,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      // Obtener el rol para verificar si existe y es del sistema
      var existingRole = await roleService.GetRoleByIdAsync(roleId, cancellationToken);
      if (existingRole == null)
      {
        return Results.Problem(
            title: "Role not found",
            detail: $"No role found with ID {roleId}",
            statusCode: StatusCodes.Status404NotFound
        );
      }

      // Verificar si es rol del sistema
      var isSystemRole = await roleService.IsSystemRoleAsync(existingRole.Name, cancellationToken);
      if (isSystemRole)
      {
        return Results.Problem(
            title: "Cannot delete system role",
            detail: "System roles (SuperAdmin, Customer) cannot be deleted",
            statusCode: StatusCodes.Status400BadRequest
        );
      }

      // Verificar si se puede eliminar (no tiene usuarios asignados)
      var canDelete = await roleService.CanDeleteRoleAsync(roleId, cancellationToken);
      if (!canDelete)
      {
        return Results.Problem(
            title: "Cannot delete role",
            detail: "This role has users assigned and cannot be deleted. Please reassign users first.",
            statusCode: StatusCodes.Status409Conflict
        );
      }

      await roleService.DeleteRoleAsync(roleId, cancellationToken);
      return Results.NoContent();
    }

    private static async Task<IResult> GetAvailableModules(
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      var modules = await roleService.GetAvailableModulesAsync(cancellationToken);
      return Results.Ok(modules);
    }

    private static async Task<IResult> GetRolePermissions(
        Guid roleId,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      var permissions = await roleService.GetRolePermissionsAsync(roleId, cancellationToken);

      if (permissions == null)
      {
        return Results.Problem(
            title: "Role not found",
            detail: $"No role found with ID {roleId}",
            statusCode: StatusCodes.Status404NotFound
        );
      }

      return Results.Ok(permissions);
    }

    private static async Task<IResult> UpdateRolePermissions(
        Guid roleId,
        [FromBody] UpdateRolePermissionsRequest request,
        IRoleService roleService,
        CancellationToken cancellationToken)
    {
      if (request.Permissions == null || !request.Permissions.Any())
      {
        return Results.Problem(
            title: "Validation failed",
            detail: "At least one module permission must be specified",
            statusCode: StatusCodes.Status400BadRequest
        );
      }

      var permissions = await roleService.UpdateRolePermissionsAsync(roleId, request, cancellationToken);

      if (permissions == null)
      {
        return Results.Problem(
            title: "Role not found",
            detail: $"No role found with ID {roleId}",
            statusCode: StatusCodes.Status404NotFound
        );
      }

      return Results.Ok(permissions);
    }
  }
}
