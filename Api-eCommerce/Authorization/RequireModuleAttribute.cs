namespace Api_eCommerce.Authorization
{
    /// <summary>
    /// Atributo para requerir acceso a un módulo específico con un permiso determinado
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class RequireModuleAttribute : Attribute
    {
        public string ModuleCode { get; }
        public string Permission { get; }  // "view", "create", "update", "delete"

        public RequireModuleAttribute(string moduleCode, string permission = "view")
        {
            ModuleCode = moduleCode;
            Permission = permission;
        }
    }
}
