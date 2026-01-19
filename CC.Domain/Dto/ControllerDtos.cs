namespace CC.Domain.Dto
{
  /// <summary>
  /// DTOs específicos para controladores de productos, tiendas y favoritos
  /// </summary>
  /// 
  // ==================== PRODUCT DTOs ====================

  /// <summary>
  /// Request para actualizar stock
  /// </summary>
  public record UpdateStockRequest(int Quantity);

  // ==================== STORES DTOs ====================

  public class MigrateLegacyStockResponse
  {
    public int MigratedProductsCount { get; set; }
    public Guid TargetStoreId { get; set; }
    public string Message { get; set; } = string.Empty;
  }

  // ==================== FAVORITES DTOs ====================

  /// <summary>
  /// Response DTO para check endpoint
  /// </summary>
  public record CheckFavoriteResponse(bool IsFavorite);
}
