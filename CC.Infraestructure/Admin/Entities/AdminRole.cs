using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
    /// <summary>
    /// Rol administrativo del sistema (SuperAdmin, TenantManager, Support, etc.)
    /// </summary>
    [Table("AdminRoles", Schema = "admin")]
    public class AdminRole
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<AdminUserRole> UserRoles { get; set; } = new List<AdminUserRole>();
    }

    /// <summary>
    /// Roles predefinidos del sistema
    /// </summary>
    public static class AdminRoleNames
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string TenantManager = "TenantManager";
        public const string Support = "Support";
        public const string Viewer = "Viewer";
    }
}
