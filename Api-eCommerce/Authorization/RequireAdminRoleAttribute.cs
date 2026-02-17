namespace Api_eCommerce.Authorization
{
  /// <summary>
  /// Atributo para requerir uno o más roles administrativos específicos
  /// Se usa como metadata en endpoints de AdminEndpoints y SuperAdminTenants
  /// </summary>
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
  public class RequireAdminRoleAttribute : Attribute
  {
    public string[] Roles { get; }

    /// <summary>
    /// Constructor que acepta uno o más roles administrativos
    /// </summary>
    /// <param name="roles">Lista de roles permitidos (SuperAdmin, TenantManager, Support, Viewer)</param>
    public RequireAdminRoleAttribute(params string[] roles)
    {
      Roles = roles ?? throw new ArgumentNullException(nameof(roles));

      if (roles.Length == 0)
      {
        throw new ArgumentException("At least one role must be specified", nameof(roles));
      }
    }
  }
}
