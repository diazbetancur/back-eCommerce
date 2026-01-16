using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Entities
{
  /// <summary>
  /// Transacción de puntos de fidelización
  /// </summary>
  [Table("LoyaltyTransactions")]
  public class LoyaltyTransaction : EntityBase<Guid>
  {
    /// <summary>
    /// Cuenta de fidelización asociada
    /// </summary>
    [Required]
    public Guid LoyaltyAccountId { get; set; }

    /// <summary>
    /// Orden asociada (si aplica)
    /// </summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// Tipo de transacción: EARN, REDEEM, ADJUST
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Puntos ganados (+) o redimidos (-)
    /// Positivo para EARN, negativo para REDEEM
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Descripción de la transacción
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Tasa de conversión usada en el momento de la transacción
    /// Snapshot para mantener histórico inmutable
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ConversionRateUsed { get; set; }

    /// <summary>
    /// Fecha de expiración de los puntos ganados
    /// Se calcula como DateCreated + PointsExpirationDays de la configuración
    /// null = sin vencimiento
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    // Navigation property
    public LoyaltyAccount LoyaltyAccount { get; set; } = null!;
  }

  /// <summary>
  /// Tipos de transacciones de loyalty
  /// </summary>
  public static class LoyaltyTransactionType
  {
    public const string Earn = "EARN";
    public const string Redeem = "REDEEM";
    public const string Adjust = "ADJUST";
  }
}
