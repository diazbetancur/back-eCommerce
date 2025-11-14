using System.ComponentModel.DataAnnotations;

namespace CC.Domain.Users
{
    /// <summary>
    /// Cuenta de usuario por tenant (credenciales y datos de autenticación)
    /// </summary>
    public class UserAccount
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string PasswordSalt { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation property
        public UserProfile? Profile { get; set; }
    }
}
