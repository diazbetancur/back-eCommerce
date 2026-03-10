using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Entities
{
  /// <summary>
  /// Productos elegibles para una recompensa de loyalty.
  /// Si no hay registros asociados, se interpreta como descuento general.
  /// </summary>
  [Table("LoyaltyRewardProducts")]
  public class LoyaltyRewardProduct
  {
    [Required]
    public Guid RewardId { get; set; }

    [Required]
    public Guid ProductId { get; set; }

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public LoyaltyReward Reward { get; set; } = null!;
  }
}
