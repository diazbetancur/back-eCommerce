namespace CC.Infraestructure.Tenant.Entities
{
  /// <summary>
  /// Physical store/location for a tenant.
  /// Allows managing inventory across multiple locations.
  /// </summary>
  public class Store
  {
    public Guid Id { get; set; }

    /// <summary>
    /// Store name (e.g., "Main Store", "Downtown Branch")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique store code (e.g., "MAIN", "BRANCH-01")
    /// Optional but recommended for integrations
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Physical address of the store
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City where store is located
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Country where store is located
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Contact phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// If true, this is the default store for new orders
    /// Only one store should be default per tenant
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// If false, store is deactivated (soft delete)
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
  }
}
