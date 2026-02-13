namespace CC.Domain.Dto;

/// <summary>
/// Catálogo completo de módulos disponibles en el sistema
/// </summary>
public record AvailableModulesResponse(List<AvailableModuleDto> Modules);

/// <summary>
/// Módulo disponible para asignar permisos
/// </summary>
public record AvailableModuleDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string? IconName,
    bool IsActive
);

/// <summary>
/// Permisos actuales de un rol
/// </summary>
public record RolePermissionsResponse(
    Guid RoleId,
    string RoleName,
    List<RoleModulePermissionDto> Permissions
);

/// <summary>
/// Request para actualizar permisos de un rol (reemplazo completo)
/// </summary>
public record UpdateRolePermissionsRequest(
    List<UpdateModulePermissionDto> Permissions
);

/// <summary>
/// Permiso a actualizar para un módulo específico
/// </summary>
public record UpdateModulePermissionDto(
    Guid ModuleId,
    bool CanView,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete
);
