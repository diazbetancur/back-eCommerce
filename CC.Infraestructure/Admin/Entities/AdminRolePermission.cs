namespace CC.Infraestructure.Admin.Entities;

/// <summary>
/// Tabla de unión entre AdminRoles y AdminPermissions
/// Define qué permisos tiene cada rol administrativo
/// </summary>
public class AdminRolePermission
{
  public Guid AdminRoleId { get; set; }
  public AdminRole AdminRole { get; set; } = null!;

  public Guid AdminPermissionId { get; set; }
  public AdminPermission AdminPermission { get; set; } = null!;

  public DateTime AssignedAt { get; set; }

  /// <summary>
  /// Usuario admin que asignó este permiso al rol
  /// </summary>
  public Guid? AssignedByUserId { get; set; }
}
