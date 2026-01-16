using System.ComponentModel.DataAnnotations;

namespace CC.Aplication.Loyalty
{
  // ==================== REDEMPTION - REQUEST DTOs ====================

  /// <summary>
  /// Request para canjear un premio
  /// </summary>
  public record RedeemRewardRequest
  {
    [Required(ErrorMessage = "El ID del premio es requerido")]
    public Guid RewardId { get; init; }
  }

  /// <summary>
  /// Request para actualizar el estado de un canje (admin)
  /// </summary>
  public record UpdateRedemptionStatusRequest
  {
    [Required(ErrorMessage = "El estado es requerido")]
    public string Status { get; init; } = string.Empty; // PENDING, APPROVED, DELIVERED, CANCELLED, EXPIRED

    [StringLength(500, ErrorMessage = "Las notas no pueden exceder 500 caracteres")]
    public string? AdminNotes { get; init; }
  }

  // ==================== REDEMPTION - RESPONSE DTOs ====================

  /// <summary>
  /// DTO de respuesta para un canje de premio
  /// </summary>
  public record LoyaltyRedemptionDto(
      Guid Id,
      Guid RewardId,
      string RewardName,
      string RewardType,
      int PointsSpent,
      string Status,
      string? CouponCode,
      DateTime RedeemedAt,
      DateTime? ExpiresAt,
      DateTime? DeliveredAt,
      string? AdminNotes,
      Guid? OrderId,
      string? OrderNumber
  );

  /// <summary>
  /// Respuesta paginada de canjes
  /// </summary>
  public record PagedLoyaltyRedemptionsResponse(
      List<LoyaltyRedemptionDto> Items,
      int TotalCount,
      int Page,
      int PageSize,
      int TotalPages
  );

  /// <summary>
  /// Query para obtener canjes
  /// </summary>
  public record GetLoyaltyRedemptionsQuery(
      int Page = 1,
      int PageSize = 20,
      string? Status = null,
      Guid? UserId = null,
      DateTime? FromDate = null,
      DateTime? ToDate = null
  );

  /// <summary>
  /// Respuesta al canjear un premio
  /// </summary>
  public record RedeemRewardResponse(
      Guid RedemptionId,
      string Message,
      int RemainingPoints,
      string? CouponCode,
      DateTime? ExpiresAt
  );
}
