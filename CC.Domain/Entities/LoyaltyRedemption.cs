using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Entities
{
  /// <summary>
  /// Canje de premio realizado por un usuario
  /// </summary>
  [Table("LoyaltyRedemptions")]
  public class LoyaltyRedemption : EntityBase<Guid>
  {
    /// <summary>
    /// Cuenta de lealtad que realizó el canje
    /// </summary>
    [Required]
    public Guid LoyaltyAccountId { get; set; }

    /// <summary>
    /// Premio canjeado
    /// </summary>
    [Required]
    public Guid RewardId { get; set; }

    /// <summary>
    /// Puntos gastados en el canje
    /// </summary>
    public int PointsSpent { get; set; }

    /// <summary>
    /// Estado del canje: PENDING, APPROVED, DELIVERED, CANCELLED, EXPIRED
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = LoyaltyRedemptionStatus.Pending;

    /// <summary>
    /// Orden asociada (si el premio generó una orden)
    /// </summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Código de cupón generado (si aplica)
    /// </summary>
    [MaxLength(50)]
    public string? CouponCode { get; set; }

    /// <summary>
    /// Fecha de canje
    /// </summary>
    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha de expiración (si aplica)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Fecha de entrega/uso (si aplica)
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Notas administrativas
    /// </summary>
    [MaxLength(500)]
    public string? AdminNotes { get; set; }

    /// <summary>
    /// Fecha de última actualización
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public LoyaltyAccount LoyaltyAccount { get; set; } = null!;
    public LoyaltyReward Reward { get; set; } = null!;
  }

  /// <summary>
  /// Estados de canje de premio
  /// </summary>
  public static class LoyaltyRedemptionStatus
  {
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Delivered = "DELIVERED";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
  }
}
