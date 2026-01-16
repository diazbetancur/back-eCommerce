namespace CC.Infraestructure.Tenant.Entities
{
  /// <summary>
  /// Tracks stock of a product at a specific store.
  /// Enables multi-location inventory management.
  /// </summary>
  public class ProductStoreStock
  {
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the product
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Reference to the store
    /// </summary>
    public Guid StoreId { get; set; }

    /// <summary>
    /// Total stock available at this store
    /// </summary>
    public int Stock { get; set; } = 0;

    /// <summary>
    /// Stock reserved by pending orders
    /// Reserved stock is included in Stock but not available for new orders
    /// </summary>
    public int ReservedStock { get; set; } = 0;

    /// <summary>
    /// Last time stock was updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Product? Product { get; set; }
    public Store? Store { get; set; }

    /// <summary>
    /// Computed property: Stock available for new orders
    /// </summary>
    public int AvailableStock => Stock - ReservedStock;
  }
}
