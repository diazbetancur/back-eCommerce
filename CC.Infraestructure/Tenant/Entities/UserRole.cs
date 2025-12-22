using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
  /// <summary>
  /// Relación N:N entre Users y Roles
  /// Un usuario puede tener múltiples roles
  /// </summary>
  [Table("UserRoles")]
  public class UserRole
  {
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
  }
}
