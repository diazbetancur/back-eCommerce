namespace CC.Domain.Dto
{
  /// <summary>
  /// Response DTO para la configuración y feature flags del tenant autenticado
  /// </summary>
  public class TenantConfigResponse
  {
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public TenantFeaturesDto Features { get; set; } = new();
  }

  /// <summary>
  /// Feature flags expuestos al frontend
  /// </summary>
  public class TenantFeaturesDto
  {
    /// <summary>
    /// Programa de lealtad habilitado (wishlist/favorites)
    /// </summary>
    public bool Loyalty { get; set; }

    /// <summary>
    /// Soporte multi-tienda (variantes de productos)
    /// </summary>
    public bool Multistore { get; set; }

    /// <summary>
    /// Pasarela de pagos Wompi habilitada
    /// </summary>
    public bool PaymentsWompiEnabled { get; set; }

    /// <summary>
    /// Permitir checkout sin registro (invitado)
    /// </summary>
    public bool AllowGuestCheckout { get; set; }

    /// <summary>
    /// Mostrar stock disponible
    /// </summary>
    public bool ShowStock { get; set; }

    /// <summary>
    /// Habilitar reseñas de productos
    /// </summary>
    public bool EnableReviews { get; set; }

    /// <summary>
    /// Búsqueda avanzada habilitada
    /// </summary>
    public bool EnableAdvancedSearch { get; set; }

    /// <summary>
    /// Analytics habilitados
    /// </summary>
    public bool EnableAnalytics { get; set; }
  }
}
