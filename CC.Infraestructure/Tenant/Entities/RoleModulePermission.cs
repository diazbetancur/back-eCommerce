using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
    /// <summary>
    /// Permisos de un rol sobre un m�dulo espec�fico
    /// Define qu� acciones puede realizar cada rol en cada m�dulo
    /// </summary>
    [Table("RoleModulePermissions")]
    public class RoleModulePermission
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid RoleId { get; set; }

        [Required]
        public Guid ModuleId { get; set; }

        public bool CanView { get; set; } = false;
        public bool CanCreate { get; set; } = false;
        public bool CanUpdate { get; set; } = false;
        public bool CanDelete { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Role Role { get; set; } = null!;
        public Module Module { get; set; } = null!;
    }
}
