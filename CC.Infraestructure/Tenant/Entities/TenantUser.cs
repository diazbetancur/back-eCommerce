using System.ComponentModel.DataAnnotations;

namespace CC.Infraestructure.Tenant.Entities
{
 public class TenantUser
 {
 public Guid Id { get; set; }
 [Required, EmailAddress]
 public string Email { get; set; }
 [Required]
 public string PasswordHash { get; set; }
 public bool IsActive { get; set; } = true;
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 }
}