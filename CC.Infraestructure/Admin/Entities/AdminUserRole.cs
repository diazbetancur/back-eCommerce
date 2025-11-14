using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
    /// <summary>
    /// Relación muchos-a-muchos entre AdminUsers y AdminRoles
    /// </summary>
    [Table("AdminUserRoles", Schema = "admin")]
    public class AdminUserRole
    {
        [Required]
        public Guid AdminUserId { get; set; }

        [Required]
        public Guid AdminRoleId { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public AdminUser AdminUser { get; set; } = null!;
        public AdminRole AdminRole { get; set; } = null!;
    }
}
