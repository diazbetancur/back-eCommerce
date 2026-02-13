namespace CC.Domain.Dto;

/// <summary>
/// Lista de roles (vista resumida)
/// </summary>
public record RoleListItemDto(
    Guid Id,
    string Name,
    string? Description,
    int UserCount,          // Cuántos usuarios tienen este rol
    int PermissionCount,    // Cuántos permisos tiene asignados
    DateTime CreatedAt
);

public record RolesResponse(List<RoleListItemDto> Roles);

/// <summary>
/// Detalle completo de un rol con permisos y usuarios
/// </summary>
public record RoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    List<RoleModulePermissionDto> Permissions,
    List<RoleUserDto> Users,
    DateTime CreatedAt
);

/// <summary>
/// Usuario asignado a un rol
/// </summary>
public record RoleUserDto(
    Guid UserId,
    string Email
);

/// <summary>
/// Request para crear un nuevo rol
/// </summary>
public record CreateRoleRequest(
    string Name,
    string? Description
);

/// <summary>
/// Request para actualizar un rol existente
/// </summary>
public record UpdateRoleRequest(
    string Name,
    string? Description
);

/// <summary>
/// Permisos de un rol sobre un módulo específico
/// </summary>
public record RoleModulePermissionDto(
    Guid ModuleId,
    string ModuleCode,
    string ModuleName,
    string? IconName,
    bool CanView,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete
);
