namespace CC.Infraestructure.Admin.Entities;

/// <summary>
/// Representa un permiso disponible en el sistema administrativo
/// Los permisos se agrupan por recursos y definen acciones específicas
/// Ejemplo: Tenants:Create, Tenants:View, Users:Delete, etc.
/// </summary>
public class AdminPermission
{
  public Guid Id { get; set; }

  /// <summary>
  /// Nombre único del permiso (ej: "Tenants:Create", "Users:View")
  /// </summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>
  /// Recurso al que aplica el permiso (ej: "Tenants", "Users", "Audit")
  /// </summary>
  public string Resource { get; set; } = string.Empty;

  /// <summary>
  /// Acción específica del permiso (ej: "Create", "View", "Update", "Delete")
  /// </summary>
  public string Action { get; set; } = string.Empty;

  /// <summary>
  /// Descripción legible del permiso
  /// </summary>
  public string? Description { get; set; }

  /// <summary>
  /// Indica si es un permiso del sistema que no puede ser eliminado
  /// </summary>
  public bool IsSystemPermission { get; set; }

  public DateTime CreatedAt { get; set; }

  // Navigation properties
  public ICollection<AdminRolePermission> RolePermissions { get; set; } = new List<AdminRolePermission>();
}

/// <summary>
/// Recursos disponibles en el sistema administrativo
/// </summary>
public static class AdminResources
{
  public const string Tenants = "Tenants";
  public const string Users = "Users";
  public const string Roles = "Roles";
  public const string Audit = "Audit";
  public const string Plans = "Plans";
  public const string Features = "Features";
  public const string System = "System";
}

/// <summary>
/// Acciones disponibles para permisos
/// </summary>
public static class AdminActions
{
  public const string Create = "Create";
  public const string View = "View";
  public const string Update = "Update";
  public const string Delete = "Delete";
  public const string ManagePlans = "ManagePlans";
  public const string ManageRoles = "ManageRoles";
  public const string Export = "Export";
  public const string ViewMetrics = "ViewMetrics";
  public const string ManageConfig = "ManageConfig";
}
