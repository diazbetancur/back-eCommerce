using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
    [Table("TenantUserRoles")]
    public class TenantUserRole
    {
        public Guid UserId { get; set; }
        public Guid RoleId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public TenantUser User { get; set; } = null!;
        public TenantRole Role { get; set; } = null!;
    }
}