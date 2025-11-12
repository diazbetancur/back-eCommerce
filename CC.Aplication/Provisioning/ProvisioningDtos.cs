using System.ComponentModel.DataAnnotations;

namespace CC.Aplication.Provisioning
{
    /// <summary>
    /// Request para inicializar aprovisionamiento de tenant
    /// </summary>
    public class InitProvisioningRequest
    {
        [Required(ErrorMessage = "El nombre del tenant es requerido")]
        [MinLength(3, ErrorMessage = "El nombre debe tener al menos 3 caracteres")]
        [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "El slug es requerido")]
        [RegularExpression("^[a-z0-9-]+$", ErrorMessage = "El slug solo puede contener letras minúsculas, números y guiones")]
        [MinLength(3, ErrorMessage = "El slug debe tener al menos 3 caracteres")]
        [MaxLength(50, ErrorMessage = "El slug no puede exceder 50 caracteres")]
        public string Slug { get; set; } = string.Empty;

        [Required(ErrorMessage = "El plan es requerido")]
        public string Plan { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response de inicialización de aprovisionamiento
    /// </summary>
    public class InitProvisioningResponse
    {
        public Guid ProvisioningId { get; set; }
        public string ConfirmToken { get; set; } = string.Empty;
        public string Next { get; set; } = "/provision/tenants/confirm";
        public string Message { get; set; } = "Aprovisionamiento iniciado. Use el token para confirmar.";
    }

    /// <summary>
    /// Response de confirmación de aprovisionamiento
    /// </summary>
    public class ConfirmProvisioningResponse
    {
        public Guid ProvisioningId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string StatusEndpoint { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response de estado de aprovisionamiento
    /// </summary>
    public class ProvisioningStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public string? TenantSlug { get; set; }
        public string? DbName { get; set; }
        public List<ProvisioningStepDto> Steps { get; set; } = new();
    }

    /// <summary>
    /// DTO de paso de aprovisionamiento
    /// </summary>
    public class ProvisioningStepDto
    {
        public string Step { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Log { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
