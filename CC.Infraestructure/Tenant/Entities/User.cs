using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
  /// <summary>
  /// Usuario unificado del tenant (admins, staff y clientes)
  /// Todos los usuarios usan esta tabla con roles diferentes
  /// </summary>
  [Table("Users")]
  public class User
  {
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indica si el usuario debe cambiar la contraseña en el próximo inicio de sesión
    /// </summary>
    public bool MustChangePassword { get; set; } = false;

    /// <summary>
    /// ID del tenant al que pertenece este usuario (para validación de ownership)
    /// NOTA: No es FK porque AdminDb está separado
    /// </summary>
    [Required]
    public Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
  }
}
