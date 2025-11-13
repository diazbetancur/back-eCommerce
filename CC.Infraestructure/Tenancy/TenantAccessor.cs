namespace CC.Infraestructure.Tenancy
{
    /// <summary>
    /// Información del tenant resuelto
    /// </summary>
    public class TenantInfo
    {
        public Guid Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string DbName { get; set; } = string.Empty;
        public string? Plan { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Interfaz para acceder a la información del tenant en el contexto actual
    /// </summary>
    public interface ITenantAccessor
    {
        TenantInfo? TenantInfo { get; }
        void SetTenant(TenantInfo tenantInfo);
        bool HasTenant { get; }
    }

    /// <summary>
    /// Implementación de acceso al tenant actual (scoped per request)
    /// </summary>
    public class TenantAccessor : ITenantAccessor
    {
        private TenantInfo? _tenantInfo;

        public TenantInfo? TenantInfo => _tenantInfo;

        public bool HasTenant => _tenantInfo != null;

        public void SetTenant(TenantInfo tenantInfo)
        {
            if (_tenantInfo != null)
            {
                throw new InvalidOperationException("Tenant already set for this request");
            }

            _tenantInfo = tenantInfo ?? throw new ArgumentNullException(nameof(tenantInfo));
        }
    }
}
