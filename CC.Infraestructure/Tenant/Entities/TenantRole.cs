using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
    [Table("TenantRoles")]
    public class TenantRole
    {
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<TenantUserRole> UserRoles { get; set; } = new List<TenantUserRole>();
        public ICollection<RoleModulePermission> ModulePermissions { get; set; } = new List<RoleModulePermission>();
    }
}