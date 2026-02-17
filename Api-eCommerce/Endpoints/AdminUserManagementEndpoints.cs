using Api_eCommerce.Authorization;
using CC.Aplication.Admin;
using CC.Infraestructure.Admin.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api_eCommerce.Endpoints
{
  /// <summary>
  /// Endpoints para gestión de usuarios administrativos
  /// Solo accesible para SuperAdmin
  /// </summary>
  public static class AdminUserManagementEndpoints
  {
    public static IEndpointRouteBuilder MapAdminUserManagementEndpoints(this IEndpointRouteBuilder app)
    {
      var superadmin = app.MapGroup("/superadmin")
          .RequireAuthorization()
          .AddEndpointFilter<AdminRoleAuthorizationFilter>()
          .WithMetadata(new RequireAdminRoleAttribute(AdminRoleNames.SuperAdmin)); // All endpoints require SuperAdmin

      // ==================== ADMIN USERS ====================

      superadmin.MapGet("/users", GetUsers)
          .WithName("GetAdminUsers")
          .WithSummary("List admin users (paginated)")
          .WithTags("SuperAdmin - User Management")
          .Produces<PagedAdminUsersResponse>(StatusCodes.Status200OK);

      superadmin.MapGet("/users/{userId:guid}", GetUserById)
          .WithName("GetAdminUserById")
          .WithSummary("Get admin user details by ID")
          .WithTags("SuperAdmin - User Management")
          .Produces<AdminUserDetailDto>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      superadmin.MapPost("/users", CreateUser)
          .WithName("CreateAdminUser")
          .WithSummary("Create new admin user")
          .WithTags("SuperAdmin - User Management")
          .Produces<AdminUserDetailDto>(StatusCodes.Status201Created)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

      superadmin.MapPut("/users/{userId:guid}", UpdateUser)
          .WithName("UpdateAdminUser")
          .WithSummary("Update admin user")
          .WithTags("SuperAdmin - User Management")
          .Produces<AdminUserDetailDto>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      superadmin.MapPut("/users/{userId:guid}/roles", UpdateUserRoles)
          .WithName("UpdateAdminUserRoles")
          .WithSummary("Update admin user roles")
          .WithTags("SuperAdmin - User Management")
          .Produces<AdminUserDetailDto>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

      superadmin.MapPatch("/users/{userId:guid}/password", UpdateUserPassword)
          .WithName("UpdateAdminUserPassword")
          .WithSummary("Update admin user password")
          .WithTags("SuperAdmin - User Management")
          .Produces(StatusCodes.Status204NoContent)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      superadmin.MapDelete("/users/{userId:guid}", DeleteUser)
          .WithName("DeleteAdminUser")
          .WithSummary("Delete admin user (soft delete)")
          .WithTags("SuperAdmin - User Management")
          .Produces(StatusCodes.Status204NoContent)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      // ==================== ADMIN ROLES ====================

      superadmin.MapGet("/admin-roles", GetAllRoles)
          .WithName("GetAdminRoles")
          .WithSummary("Get all available admin roles")
          .WithTags("SuperAdmin - Roles")
          .Produces<List<AdminRoleDetailDto>>(StatusCodes.Status200OK);

      superadmin.MapGet("/admin-roles/{roleId:guid}", GetRoleById)
          .WithName("GetAdminRoleById")
          .WithSummary("Get admin role details by ID")
          .WithTags("SuperAdmin - Roles")
          .Produces<AdminRoleDetailDto>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      superadmin.MapPost("/admin-roles", CreateRole)
          .WithName("CreateAdminRole")
          .WithSummary("Create new admin role")
          .WithTags("SuperAdmin - Roles")
          .Produces<AdminRoleDetailDto>(StatusCodes.Status201Created)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

      superadmin.MapPut("/admin-roles/{roleId:guid}", UpdateRole)
          .WithName("UpdateAdminRole")
          .WithSummary("Update admin role")
          .WithTags("SuperAdmin - Roles")
          .Produces<AdminRoleDetailDto>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      superadmin.MapDelete("/admin-roles/{roleId:guid}", DeleteRole)
          .WithName("DeleteAdminRole")
          .WithSummary("Delete admin role")
          .WithTags("SuperAdmin - Roles")
          .Produces(StatusCodes.Status204NoContent)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

      // ==================== ADMIN PERMISSIONS ====================

      superadmin.MapGet("/permissions", GetAvailablePermissions)
          .WithName("GetAvailableAdminPermissions")
          .WithSummary("Get all available admin permissions grouped by resource")
          .WithTags("SuperAdmin - Permissions")
          .Produces<AvailableAdminPermissionsResponse>(StatusCodes.Status200OK);

      superadmin.MapGet("/admin-roles/{roleId:guid}/permissions", GetRolePermissions)
          .WithName("GetAdminRolePermissions")
          .WithSummary("Get permissions assigned to a role")
          .WithTags("SuperAdmin - Permissions")
          .Produces<AdminRolePermissionsResponse>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

      superadmin.MapPut("/admin-roles/{roleId:guid}/permissions", UpdateRolePermissions)
          .WithName("UpdateAdminRolePermissions")
          .WithSummary("Update permissions assigned to a role")
          .WithTags("SuperAdmin - Permissions")
          .Produces<AdminRolePermissionsResponse>(StatusCodes.Status200OK)
          .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

      return app;
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> GetUsers(
        [AsParameters] AdminUserListQuery query,
        IAdminUserManagementService service)
    {
      try
      {
        var result = await service.GetUsersAsync(query);
        return Results.Ok(result);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while retrieving users: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> GetUserById(
        Guid userId,
        IAdminUserManagementService service)
    {
      try
      {
        var result = await service.GetUserByIdAsync(userId);

        if (result == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status404NotFound,
              title: "Not Found",
              detail: $"Admin user with id '{userId}' not found"
          );
        }

        return Results.Ok(result);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while retrieving user: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> CreateUser(
        [FromBody] CreateAdminUserRequest request,
        IAdminUserManagementService service,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var result = await service.CreateUserAsync(request);

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            AuditActions.UserCreated,
            AuditResourceTypes.AdminUser,
            result.Id.ToString(),
            new { result.Email, result.FullName, RoleNames = request.RoleNames },
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.Created($"/superadmin/users/{result.Id}", result);
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while creating user: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> UpdateUser(
        Guid userId,
        [FromBody] UpdateAdminUserRequest request,
        IAdminUserManagementService service,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var result = await service.UpdateUserAsync(userId, request);

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        var action = request.IsActive.HasValue
            ? (request.IsActive.Value ? AuditActions.UserActivated : AuditActions.UserDeactivated)
            : AuditActions.UserUpdated;

        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            action,
            AuditResourceTypes.AdminUser,
            userId.ToString(),
            request,
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.Ok(result);
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
            detail: $"An error occurred while updating user: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> UpdateUserRoles(
        Guid userId,
        [FromBody] UpdateAdminUserRolesRequest request,
        IAdminUserManagementService service,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var result = await service.UpdateUserRolesAsync(userId, request);

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            AuditActions.UserRolesUpdated,
            AuditResourceTypes.AdminUser,
            userId.ToString(),
            new { NewRoles = request.RoleNames },
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.Ok(result);
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while updating user roles: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> UpdateUserPassword(
        Guid userId,
        [FromBody] UpdateAdminPasswordRequest request,
        IAdminUserManagementService service,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var result = await service.UpdateUserPasswordAsync(userId, request);

        if (!result)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status404NotFound,
              title: "Not Found",
              detail: $"Admin user with id '{userId}' not found"
          );
        }

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            AuditActions.UserPasswordChanged,
            AuditResourceTypes.AdminUser,
            userId.ToString(),
            null, // No incluir la contraseña en los detalles
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.NoContent();
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while updating password: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> DeleteUser(
        Guid userId,
        IAdminUserManagementService service,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var result = await service.DeleteUserAsync(userId);

        if (!result)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status404NotFound,
              title: "Not Found",
              detail: $"Admin user with id '{userId}' not found"
          );
        }

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            AuditActions.UserDeleted,
            AuditResourceTypes.AdminUser,
            userId.ToString(),
            null,
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.NoContent();
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while deleting user: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> GetAllRoles(
        IAdminRoleManagementService roleService)
    {
      try
      {
        var result = await roleService.GetAllRolesAsync();
        return Results.Ok(result);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while retrieving roles: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> GetRoleById(
        Guid roleId,
        IAdminRoleManagementService roleService)
    {
      try
      {
        var role = await roleService.GetRoleByIdAsync(roleId);

        if (role == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status404NotFound,
              title: "Not Found",
              detail: $"Admin role with id '{roleId}' not found"
          );
        }

        return Results.Ok(role);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while retrieving role: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> CreateRole(
        [FromBody] CreateAdminRoleRequest request,
        IAdminRoleManagementService roleService,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var role = await roleService.CreateRoleAsync(request);

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            "RoleCreated",
            "AdminRole",
            role.Id.ToString(),
            new { roleName = role.Name, permissionCount = role.Permissions.Count },
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.Created($"/superadmin/admin-roles/{role.Id}", role);
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while creating role: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> UpdateRole(
        Guid roleId,
        [FromBody] UpdateAdminRoleRequest request,
        IAdminRoleManagementService roleService,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var role = await roleService.UpdateRoleAsync(roleId, request);

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            "RoleUpdated",
            "AdminRole",
            roleId.ToString(),
            new { roleName = role.Name, updates = request },
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.Ok(role);
      }
      catch (KeyNotFoundException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: ex.Message
        );
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while updating role: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> DeleteRole(
        Guid roleId,
        IAdminRoleManagementService roleService,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var role = await roleService.GetRoleByIdAsync(roleId);
        if (role == null)
        {
          return Results.Problem(
              statusCode: StatusCodes.Status404NotFound,
              title: "Not Found",
              detail: $"Admin role with id '{roleId}' not found"
          );
        }

        await roleService.DeleteRoleAsync(roleId);

        // Registrar auditoría
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            "RoleDeleted",
            "AdminRole",
            roleId.ToString(),
            new { roleName = role.Name },
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.NoContent();
      }
      catch (KeyNotFoundException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: ex.Message
        );
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while deleting role: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> GetAvailablePermissions(
        IAdminRoleManagementService roleService)
    {
      try
      {
        var permissions = await roleService.GetAvailablePermissionsAsync();
        return Results.Ok(permissions);
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while retrieving permissions: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> GetRolePermissions(
        Guid roleId,
        IAdminRoleManagementService roleService)
    {
      try
      {
        var permissions = await roleService.GetRolePermissionsAsync(roleId);
        return Results.Ok(permissions);
      }
      catch (KeyNotFoundException ex)
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
            detail: $"An error occurred while retrieving role permissions: {ex.Message}"
        );
      }
    }

    private static async Task<IResult> UpdateRolePermissions(
        Guid roleId,
        [FromBody] UpdateAdminRolePermissionsRequest request,
        IAdminRoleManagementService roleService,
        IAdminAuditService auditService,
        HttpContext httpContext)
    {
      try
      {
        var (currentUserId, currentUserEmail) = AdminAuditEndpoints.GetCurrentAdminUser(httpContext.User);

        var permissions = await roleService.UpdateRolePermissionsAsync(roleId, request, currentUserId);

        // Registrar auditoría
        await auditService.LogActionAsync(
            currentUserId,
            currentUserEmail,
            "RolePermissionsUpdated",
            "AdminRole",
            roleId.ToString(),
            new
            {
              roleName = permissions.RoleName,
              permissionCount = permissions.Permissions.Count,
              permissionIds = request.PermissionIds
            },
            AdminAuditEndpoints.GetIpAddress(httpContext),
            AdminAuditEndpoints.GetUserAgent(httpContext)
        );

        return Results.Ok(permissions);
      }
      catch (KeyNotFoundException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Not Found",
            detail: ex.Message
        );
      }
      catch (InvalidOperationException ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Bad Request",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        return Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: $"An error occurred while updating role permissions: {ex.Message}"
        );
      }
    }
  }
}
