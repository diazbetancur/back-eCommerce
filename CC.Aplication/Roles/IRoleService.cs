using CC.Domain.Dto;

namespace CC.Aplication.Roles;

/// <summary>
/// Servicio para gestión completa de roles y permisos
/// </summary>
public interface IRoleService
{
  // ==================== CRUD ROLES ====================

  /// <summary>
  /// Obtiene la lista de todos los roles del tenant
  /// </summary>
  Task<RolesResponse> GetRolesAsync(CancellationToken ct = default);

  /// <summary>
  /// Obtiene el detalle completo de un rol por ID
  /// </summary>
  Task<RoleDetailDto> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default);

  /// <summary>
  /// Crea un nuevo rol
  /// </summary>
  Task<RoleDetailDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default);

  /// <summary>
  /// Actualiza nombre y descripción de un rol existente
  /// </summary>
  Task<RoleDetailDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken ct = default);

  /// <summary>
  /// Elimina un rol (solo si no tiene usuarios asignados)
  /// </summary>
  Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);

  // ==================== GESTIÓN DE PERMISOS ====================

  /// <summary>
  /// Obtiene el catálogo completo de módulos disponibles
  /// </summary>
  Task<AvailableModulesResponse> GetAvailableModulesAsync(CancellationToken ct = default);

  /// <summary>
  /// Obtiene los permisos actuales de un rol
  /// </summary>
  Task<RolePermissionsResponse> GetRolePermissionsAsync(Guid roleId, CancellationToken ct = default);

  /// <summary>
  /// Actualiza los permisos de un rol (reemplazo completo)
  /// </summary>
  Task<RolePermissionsResponse> UpdateRolePermissionsAsync(
      Guid roleId,
      UpdateRolePermissionsRequest request,
      CancellationToken ct = default);

  // ==================== VALIDACIONES ====================

  /// <summary>
  /// Verifica si un rol puede ser eliminado
  /// </summary>
  Task<bool> CanDeleteRoleAsync(Guid roleId, CancellationToken ct = default);

  /// <summary>
  /// Verifica si un rol es del sistema (no editable/eliminable)
  /// </summary>
  Task<bool> IsSystemRoleAsync(string roleName, CancellationToken ct = default);
}
