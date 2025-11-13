using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
    [Table("TenantProvisionings", Schema = "admin")]
    public class TenantProvisioning
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid TenantId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Step { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; }
        
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Navigation property
        public Tenant? Tenant { get; set; }
    }
}
