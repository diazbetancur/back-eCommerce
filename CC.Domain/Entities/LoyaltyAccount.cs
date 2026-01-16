using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Entities
{
  /// <summary>
  /// Cuenta de puntos de fidelización de un usuario
  /// </summary>
  [Table("LoyaltyAccounts")]
  public class LoyaltyAccount : EntityBase<Guid>
  {
    /// <summary>
    /// Usuario propietario de la cuenta (único)
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Balance actual de puntos
    /// </summary>
    public int PointsBalance { get; set; } = 0;

    /// <summary>
    /// Fecha de última actualización
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<LoyaltyTransaction> Transactions { get; set; } = new List<LoyaltyTransaction>();
  }
}
