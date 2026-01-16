using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Entities
{
  /// <summary>
  /// Configuración del programa de lealtad por tenant
  /// </summary>
  [Table("LoyaltyConfigurations")]
  public class LoyaltyConfiguration : EntityBase<Guid>
  {
    /// <summary>
    /// Tasa de conversión: cuántos puntos se otorgan por cada dólar gastado
    /// Ejemplo: 10 = 10 puntos por cada $1
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ConversionRate { get; set; } = 10m;

    /// <summary>
    /// Días de vigencia de los puntos desde que se ganan
    /// null = sin vencimiento
    /// </summary>
    public int? PointsExpirationDays { get; set; }

    /// <summary>
    /// Indica si el programa de lealtad está activo
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Monto mínimo de compra para ganar puntos
    /// null = sin mínimo
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinPurchaseForPoints { get; set; }

    /// <summary>
    /// Fecha de última actualización
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
  }
}
