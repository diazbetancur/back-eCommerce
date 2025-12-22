using Api_eCommerce.Authorization;

namespace Api_eCommerce.Extensions
{
  /// <summary>
  /// Extension methods para autorización basada en módulos y permisos
  /// </summary>
  public static class AuthorizationEndpointExtensions
  {
    /// <summary>
    /// Requiere que el usuario tenga permiso sobre un módulo específico
    /// </summary>
    /// <param name="builder">Route builder</param>
    /// <param name="moduleCode">Código del módulo (ej: "catalog", "orders")</param>
    /// <param name="canView">Permiso de ver</param>
    /// <param name="canCreate">Permiso de crear</param>
    /// <param name="canUpdate">Permiso de actualizar</param>
    /// <param name="canDelete">Permiso de eliminar</param>
    public static RouteHandlerBuilder RequireModule(
        this RouteHandlerBuilder builder,
        string moduleCode,
        bool canView = false,
        bool canCreate = false,
        bool canUpdate = false,
        bool canDelete = false)
    {
      // Determinar el tipo de permiso requerido
      string permission = "view"; // Default

      if (canDelete) permission = "delete";
      else if (canUpdate) permission = "update";
      else if (canCreate) permission = "create";
      else if (canView) permission = "view";

      return builder.WithMetadata(new RequireModuleAttribute(moduleCode, permission));
    }
  }
}
