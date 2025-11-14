using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Loyalty
{
    /// <summary>
    /// Cuenta de puntos de fidelización de un usuario
    /// </summary>
    [Table("LoyaltyAccounts")]
    public class LoyaltyAccount
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Usuario propietario de la cuenta (único)
        /// </summary>
        [Required]
        public Guid UserId { get; set; }

        /// <summary>
        /// Balance actual de puntos
        /// </summary>
        public int PointsBalance { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<LoyaltyTransaction> Transactions { get; set; } = new List<LoyaltyTransaction>();
    }
}
