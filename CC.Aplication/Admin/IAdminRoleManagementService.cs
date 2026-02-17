namespace CC.Aplication.Admin;

/// <summary>
/// Servicio para gestión de roles administrativos y sus permisos
/// </summary>
public interface IAdminRoleManagementService
{
  // ==================== ROLES ====================

  /// <summary>
  /// Obtiene todos los roles administrativos con conteo de usuarios
  /// </summary>
  Task<List<AdminRoleDetailDto>> GetAllRolesAsync();

  /// <summary>
  /// Obtiene detalles de un rol específico por ID
  /// </summary>
  Task<AdminRoleDetailDto?> GetRoleByIdAsync(Guid roleId);

  /// <summary>
  /// Crea un nuevo rol administrativo
  /// </summary>
  Task<AdminRoleDetailDto> CreateRoleAsync(CreateAdminRoleRequest request);

  /// <summary>
  /// Actualiza la información de un rol
  /// </summary>
  Task<AdminRoleDetailDto> UpdateRoleAsync(Guid roleId, UpdateAdminRoleRequest request);

  /// <summary>
  /// Elimina un rol administrativo (solo si no tiene usuarios asignados y no es rol del sistema)
  /// </summary>
  Task<bool> DeleteRoleAsync(Guid roleId);

  /// <summary>
  /// Verifica si un rol puede ser eliminado
  /// </summary>
  Task<(bool CanDelete, string? Reason)> CanDeleteRoleAsync(Guid roleId);

  // ==================== PERMISOS ====================

  /// <summary>
  /// Obtiene todos los permisos disponibles agrupados por recurso
  /// </summary>
  Task<AvailableAdminPermissionsResponse> GetAvailablePermissionsAsync();

  /// <summary>
  /// Obtiene los permisos asignados a un rol específico
  /// </summary>
  Task<AdminRolePermissionsResponse> GetRolePermissionsAsync(Guid roleId);

  /// <summary>
  /// Actualiza los permisos de un rol
  /// </summary>
  Task<AdminRolePermissionsResponse> UpdateRolePermissionsAsync(
      Guid roleId,
      UpdateAdminRolePermissionsRequest request,
      Guid? assignedByUserId = null
  );

  /// <summary>
  /// Sincroniza los permisos del sistema (crea los que faltan en la BD)
  /// </summary>
  Task EnsureSystemPermissionsAsync();
}
