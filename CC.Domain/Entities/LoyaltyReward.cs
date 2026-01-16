using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Entities
{
  /// <summary>
  /// Premio o recompensa del programa de lealtad
  /// Configurable por el administrador del tenant
  /// </summary>
  [Table("LoyaltyRewards")]
  public class LoyaltyReward : EntityBase<Guid>
  {
    /// <summary>
    /// Nombre del premio
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del premio
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Puntos necesarios para canjear
    /// </summary>
    [Required]
    public int PointsCost { get; set; }

    /// <summary>
    /// Tipo de premio: PRODUCT, DISCOUNT_PERCENTAGE, DISCOUNT_FIXED, FREE_SHIPPING
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string RewardType { get; set; } = string.Empty;

    /// <summary>
    /// ID del producto asociado (si RewardType = PRODUCT)
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Valor del descuento (% o monto fijo según RewardType)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountValue { get; set; }

    /// <summary>
    /// URL de la imagen del premio
    /// </summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Si el premio está activo y disponible
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Stock disponible (null = ilimitado)
    /// </summary>
    public int? Stock { get; set; }

    /// <summary>
    /// Días de validez del cupón generado (si aplica)
    /// </summary>
    public int? ValidityDays { get; set; }

    /// <summary>
    /// Orden de visualización
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// Fecha de última actualización
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<LoyaltyRedemption> Redemptions { get; set; } = new List<LoyaltyRedemption>();
  }
}
