using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
  /// <summary>
  /// Registro de auditoría para acciones administrativas
  /// Permite rastrear todas las operaciones críticas del sistema
  /// </summary>
  [Table("AdminAuditLogs", Schema = "admin")]
  public class AdminAuditLog
  {
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Usuario admin que realizó la acción
    /// </summary>
    [Required]
    public Guid AdminUserId { get; set; }

    /// <summary>
    /// Email del usuario (desnormalizado para histórico)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string AdminUserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de acción realizada
    /// Ejemplos: UserCreated, UserUpdated, UserDeleted, RolesUpdated, PasswordChanged, TenantCreated, TenantDeleted, etc.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de recurso afectado
    /// Ejemplos: AdminUser, Tenant, Plan, Feature, etc.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>
    /// ID del recurso afectado (si aplica)
    /// </summary>
    [MaxLength(100)]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Detalles adicionales en formato JSON
    /// Puede incluir: valores anteriores, nuevos valores, roles modificados, etc.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Details { get; set; }

    /// <summary>
    /// Dirección IP desde donde se realizó la acción
    /// </summary>
    [MaxLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }

    /// <summary>
    /// User Agent del navegador/cliente
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp de la acción
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey(nameof(AdminUserId))]
    public AdminUser? AdminUser { get; set; }
  }
}
