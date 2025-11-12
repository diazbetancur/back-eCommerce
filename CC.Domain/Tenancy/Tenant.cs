using System;
using System.ComponentModel.DataAnnotations;

namespace CC.Domain.Tenancy
{
    /// <summary>
    /// Entidad que representa un tenant en la Admin DB
    /// </summary>
    public class Tenant
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Identificador único del tenant (usado en URLs, subdominios, etc.)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del tenant
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Plan del tenant (Basic, Premium, Enterprise, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? Plan { get; set; }

        /// <summary>
        /// Nombre de la base de datos del tenant
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string DbName { get; set; } = string.Empty;

        /// <summary>
        /// Estado del tenant (Pending, Active, Suspended, Failed)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Feature flags en formato JSON
        /// </summary>
        public string? FeatureFlagsJson { get; set; }

        /// <summary>
        /// Fecha de creación del tenant
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última actualización
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Último error durante el aprovisionamiento o operación
        /// </summary>
        public string? LastError { get; set; }

        /// <summary>
        /// Relación con el historial de aprovisionamiento
        /// </summary>
        public virtual ICollection<TenantProvisioning> ProvisioningHistory { get; set; } = new List<TenantProvisioning>();
    }
}
