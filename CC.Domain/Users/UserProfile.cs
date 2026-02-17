using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Users
{
    /// <summary>
    /// Perfil de usuario con datos personales
    /// </summary>
    public class UserProfile
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PhoneNumber { get; set; }

        [MaxLength(50)]
        public string? DocumentType { get; set; }

        [MaxLength(100)]
        public string? DocumentNumber { get; set; }

        public DateTime? BirthDate { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }
    }
}
