using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
  /// <summary>
  /// Rol del tenant (SuperAdmin, Customer, etc.)
  /// Los tenants pueden crear roles personalizados
  /// </summary>
  [Table("Roles")]
  public class Role
  {
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RoleModulePermission> ModulePermissions { get; set; } = new List<RoleModulePermission>();
  }
}
