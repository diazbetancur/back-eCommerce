using CC.Domain.Dto;

namespace CC.Aplication.Users;

/// <summary>
/// Servicio para gestión avanzada de usuarios del tenant
/// </summary>
public interface IUserManagementService
{
  /// <summary>
  /// Obtiene el detalle completo de un usuario por ID
  /// </summary>
  Task<TenantUserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);

  /// <summary>
  /// Actualiza los roles de un usuario con protección de lockout
  /// </summary>
  /// <remarks>
  /// Protege contra lockout: verifica que el admin actual no se quite su propio rol de admin
  /// </remarks>
  Task<TenantUserDetailDto?> UpdateUserRolesAsync(
      Guid userId,
      UpdateUserRolesRequest request,
      Guid currentAdminUserId,
      CancellationToken ct = default);

  /// <summary>
  /// Elimina (soft delete) un usuario del tenant
  /// </summary>
  Task<bool> DeleteUserAsync(Guid userId, CancellationToken ct = default);

  /// <summary>
  /// Activa o desactiva un usuario
  /// </summary>
  Task<TenantUserDetailDto?> UpdateUserActiveStatusAsync(
      Guid userId,
      UpdateUserActiveStatusRequest request,
      CancellationToken ct = default);

  /// <summary>
  /// Verifica si un usuario tiene al menos un rol administrativo
  /// </summary>
  Task<bool> HasAdminRoleAsync(Guid userId, CancellationToken ct = default);
}
