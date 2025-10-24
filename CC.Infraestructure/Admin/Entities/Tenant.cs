using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
 public enum TenantStatus
 {
 Pending =0,
 Seeding =1,
 Ready =2,
 Suspended =3,
 Failed =4
 }

 [Table("Tenants", Schema = "admin")] 
 public class Tenant
 {
 [Key]
 public Guid Id { get; set; }
 [Required]
 [MaxLength(100)]
 public string Slug { get; set; }
 [Required]
 [MaxLength(200)]
 public string Name { get; set; }
 public TenantStatus Status { get; set; }
 [Required]
 public string EncryptedConnection { get; set; }
 public Guid? PlanId { get; set; }
 public Plan? Plan { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime? UpdatedAt { get; set; }
 public string? LastError { get; set; }
 
 // CSV de or�genes permitidos para CORS por tenant (e.g. "https://app.com,http://localhost:5173")
 public string? AllowedOrigins { get; set; }
 }
}