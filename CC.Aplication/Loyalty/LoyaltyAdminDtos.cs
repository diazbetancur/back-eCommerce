using System.ComponentModel.DataAnnotations;

namespace CC.Aplication.Loyalty
{
  // ==================== ADMIN - MANUAL POINTS ADJUSTMENT ====================

  /// <summary>
  /// Request para ajustar puntos manualmente (admin)
  /// </summary>
  public record AdjustPointsRequest
  {
    [Required(ErrorMessage = "El ID del usuario es requerido")]
    public Guid UserId { get; init; }

    [Required(ErrorMessage = "Los puntos son requeridos")]
    [Range(-100000, 100000, ErrorMessage = "Los puntos deben estar entre -100000 y 100000")]
    public int Points { get; init; }

    [Required(ErrorMessage = "El tipo de ajuste es requerido")]
    public string TransactionType { get; init; } = string.Empty; // EARN, REDEEM, ADJUST

    [Required(ErrorMessage = "La razón es requerida")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "La razón debe tener entre 5 y 500 caracteres")]
    public string Reason { get; init; } = string.Empty;
  }

  /// <summary>
  /// Respuesta al ajustar puntos manualmente
  /// </summary>
  public record AdjustPointsResponse(
      Guid TransactionId,
      int PointsAdjusted,
      int NewBalance,
      string Message
  );
}
