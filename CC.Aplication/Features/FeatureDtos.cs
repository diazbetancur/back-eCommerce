using System.ComponentModel.DataAnnotations;

namespace CC.Aplication.Features
{
    /// <summary>
    /// Response con feature flags de un tenant
    /// </summary>
    public class TenantFeaturesResponse
    {
        public Guid TenantId { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public bool UsingDefaults { get; set; }
        public object Features { get; set; } = new();
    }

    /// <summary>
    /// Request para actualizar feature flags
    /// </summary>
    public class UpdateTenantFeaturesRequest
    {
        [Required]
        public object Features { get; set; } = new();
    }

    /// <summary>
    /// DTO para verificar una feature específica
    /// </summary>
    public class FeatureCheckResponse
    {
        public string FeatureKey { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public object? Value { get; set; }
    }
}
