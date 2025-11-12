using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Tenancy
{
    /// <summary>
    /// Entidad que representa el historial de aprovisionamiento de un tenant
    /// Registra cada paso del proceso: CreateDatabase, Migrate, Seed
    /// </summary>
    public class TenantProvisioning
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// ID del tenant asociado
        /// </summary>
        [Required]
        public Guid TenantId { get; set; }

        /// <summary>
        /// Paso del aprovisionamiento: CreateDatabase, Migrate, Seed
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Step { get; set; } = string.Empty;

        /// <summary>
        /// Estado del paso: Pending, InProgress, Success, Failed
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// Mensaje o detalle del paso
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Fecha de inicio del paso
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de finalización del paso
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Mensaje de error si el paso falló
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Relación con el tenant
        /// </summary>
        [ForeignKey(nameof(TenantId))]
        public virtual Tenant Tenant { get; set; } = null!;
    }
}
