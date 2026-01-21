namespace CC.Domain.Dto
{
  public class StoreDto
  {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
  }

  public class CreateStoreRequest
  {
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
    public Guid? CopyInventoryFromStoreId { get; set; } // Copiar inventario de otra tienda
  }

  public class UpdateStoreRequest
  {
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
  }

  public class ProductStoreStockDto
  {
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid StoreId { get; set; }
    public string? StoreName { get; set; }
    public int Stock { get; set; }
    public int ReservedStock { get; set; }
    public int AvailableStock { get; set; }
    public DateTime UpdatedAt { get; set; }
  }

  /// <summary>
  /// DTO para listar stock de productos en una tienda específica (admin UI)
  /// </summary>
  public class StoreProductStockDto
  {
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int ReservedStock { get; set; }
    public int AvailableStock { get; set; }
    public DateTime UpdatedAt { get; set; }
  }

  /// <summary>
  /// Request para actualizar stock de un producto en una tienda
  /// </summary>
  public class UpdateStoreStockRequest
  {
    public int Stock { get; set; }
  }

  public class UpdateProductStoreStockRequest
  {
    public Guid StoreId { get; set; }
    public int Stock { get; set; }
  }

  public class StockAvailabilityRequest
  {
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public Guid? StoreId { get; set; }
  }

  public class StockAvailabilityResponse
  {
    public bool IsAvailable { get; set; }
    public int AvailableStock { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? StoreId { get; set; }
    public bool UsedLegacyStock { get; set; }
  }

  public class MigrateStockToStoresRequest
  {
    public Guid DefaultStoreId { get; set; }
  }

  public class InitialStoreStockDto
  {
    public Guid StoreId { get; set; }
    public int Stock { get; set; }
  }
}
