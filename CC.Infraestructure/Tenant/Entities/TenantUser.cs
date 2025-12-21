using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
    [Table("TenantUsers")]
    public class TenantUser
    {
        public Guid Id { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Indica si el usuario debe cambiar la contraseña en el próximo inicio de sesión
        /// </summary>
        public bool MustChangePassword { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<TenantUserRole> UserRoles { get; set; } = new List<TenantUserRole>();
    }
}