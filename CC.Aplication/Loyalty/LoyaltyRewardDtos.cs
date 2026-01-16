using System.ComponentModel.DataAnnotations;

namespace CC.Aplication.Loyalty
{
  // ==================== LOYALTY REWARDS - REQUEST DTOs ====================

  /// <summary>
  /// Request para crear un premio de lealtad
  /// </summary>
  public record CreateLoyaltyRewardRequest
  {
    [Required(ErrorMessage = "El nombre del premio es requerido")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 200 caracteres")]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción no puede exceder 1000 caracteres")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Los puntos requeridos son obligatorios")]
    [Range(1, int.MaxValue, ErrorMessage = "Los puntos deben ser mayor a 0")]
    public int PointsCost { get; init; }

    [Required(ErrorMessage = "El tipo de premio es requerido")]
    public string RewardType { get; init; } = string.Empty; // PRODUCT, DISCOUNT_PERCENTAGE, DISCOUNT_FIXED, FREE_SHIPPING

    public Guid? ProductId { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "El valor del descuento debe ser positivo")]
    public decimal? DiscountValue { get; init; }

    [Url(ErrorMessage = "La URL de imagen no es válida")]
    public string? ImageUrl { get; init; }

    public bool IsActive { get; init; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "El stock debe ser positivo")]
    public int? Stock { get; init; }

    [Range(1, 365, ErrorMessage = "Los días de validez deben estar entre 1 y 365")]
    public int? ValidityDays { get; init; }

    public int DisplayOrder { get; init; } = 0;
  }

  /// <summary>
  /// Request para actualizar un premio de lealtad
  /// </summary>
  public record UpdateLoyaltyRewardRequest
  {
    [Required(ErrorMessage = "El nombre del premio es requerido")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 200 caracteres")]
    public string Name { get; init; } = string.Empty;

    [StringLength(1000, ErrorMessage = "La descripción no puede exceder 1000 caracteres")]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Los puntos requeridos son obligatorios")]
    [Range(1, int.MaxValue, ErrorMessage = "Los puntos deben ser mayor a 0")]
    public int PointsCost { get; init; }

    [Required(ErrorMessage = "El tipo de premio es requerido")]
    public string RewardType { get; init; } = string.Empty;

    public Guid? ProductId { get; init; }

    [Range(0, double.MaxValue, ErrorMessage = "El valor del descuento debe ser positivo")]
    public decimal? DiscountValue { get; init; }

    [Url(ErrorMessage = "La URL de imagen no es válida")]
    public string? ImageUrl { get; init; }

    public bool IsActive { get; init; }

    [Range(0, int.MaxValue, ErrorMessage = "El stock debe ser positivo")]
    public int? Stock { get; init; }

    [Range(1, 365, ErrorMessage = "Los días de validez deben estar entre 1 y 365")]
    public int? ValidityDays { get; init; }

    public int DisplayOrder { get; init; }
  }

  // ==================== LOYALTY REWARDS - RESPONSE DTOs ====================

  /// <summary>
  /// DTO de respuesta para un premio de lealtad
  /// </summary>
  public record LoyaltyRewardDto(
      Guid Id,
      string Name,
      string? Description,
      int PointsCost,
      string RewardType,
      Guid? ProductId,
      string? ProductName,
      decimal? DiscountValue,
      string? ImageUrl,
      bool IsActive,
      int? Stock,
      int? ValidityDays,
      int DisplayOrder,
      DateTime CreatedAt,
      DateTime UpdatedAt
  );

  /// <summary>
  /// Respuesta paginada de premios
  /// </summary>
  public record PagedLoyaltyRewardsResponse(
      List<LoyaltyRewardDto> Items,
      int TotalCount,
      int Page,
      int PageSize,
      int TotalPages
  );

  /// <summary>
  /// Query para obtener premios
  /// </summary>
  public record GetLoyaltyRewardsQuery(
      int Page = 1,
      int PageSize = 20,
      bool? IsActive = null,
      string? RewardType = null
  );
}
