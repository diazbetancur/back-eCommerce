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

    [StringLength(100, ErrorMessage = "El número de ticket no puede exceder 100 caracteres")]
    public string? TicketNumber { get; init; }
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

  public record GetManualPointAdjustmentsQuery(
    int Page = 1,
    int PageSize = 20,
    Guid? UserId = null,
    Guid? AdjustedByUserId = null,
    string? TicketNumber = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? Search = null
  );

  public record ManualPointAdjustmentItemDto(
    Guid TransactionId,
    Guid UserId,
    string UserEmail,
    Guid? AdjustedByUserId,
    string? AdjustedByEmail,
    int Points,
    string TransactionType,
    string? Observations,
    string? TicketNumber,
    DateTime? ExpiresAt,
    DateTime CreatedAt
  );

  public record PagedManualPointAdjustmentsResponse(
    List<ManualPointAdjustmentItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
  );
}
