namespace CC.Domain.Dto
{
  /// <summary>
  /// DTOs para configuración pública del tenant
  /// </summary>

  public record PublicTenantConfigResponse
  {
    public TenantInfo Tenant { get; init; } = new();
    public string Locale { get; init; } = "es-CO";
    public string Currency { get; init; } = "COP";
    public string CurrencySymbol { get; init; } = "$";
    public decimal TaxRate { get; init; }
    public ThemeInfo Theme { get; init; } = new();
    public Dictionary<string, bool> Features { get; init; } = new();
    public AppFeaturesInfo AppFeatures { get; init; } = new();
    public ContactInfo Contact { get; init; } = new();
    public SocialInfo Social { get; init; } = new();
    public SeoInfo Seo { get; init; } = new();
    public MessagesInfo Messages { get; init; } = new();
  }

  public record TenantInfo
  {
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = "Ready";
    public string Plan { get; init; } = "free";
    public BrandingInfo Branding { get; init; } = new();
  }

  public record BrandingInfo
  {
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string PrimaryColor { get; init; } = "#3b82f6";
    public string SecondaryColor { get; init; } = "#1e40af";
    public string AccentColor { get; init; } = "#10b981";
    public string BackgroundColor { get; init; } = "#ffffff";
  }

  public record ThemeInfo
  {
    public string Primary { get; init; } = "#3b82f6";
    public string Accent { get; init; } = "#10b981";
    public string Background { get; init; } = "#ffffff";
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
  }

  public record SeoInfo
  {
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }
  }

  public record ContactInfo
  {
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? WhatsApp { get; init; }
  }

  public record SocialInfo
  {
    public string? Facebook { get; init; }
    public string? Instagram { get; init; }
    public string? Twitter { get; init; }
    public string? TikTok { get; init; }
  }

  public record AppFeaturesInfo
  {
    public bool EnableCart { get; init; } = true;
    public bool EnableWishlist { get; init; } = true;
    public bool EnableReviews { get; init; } = false;
    public bool EnableLoyalty { get; init; } = true;
    public bool EnableGuestCheckout { get; init; } = true;
    public bool EnableNotifications { get; init; } = true;
  }

  public record MessagesInfo
  {
    public string Welcome { get; init; } = "¡Bienvenido!";
    public string CartEmpty { get; init; } = "Tu carrito está vacío";
    public string CheckoutSuccess { get; init; } = "¡Gracias por tu compra!";
    public string OutOfStock { get; init; } = "Producto agotado";
  }
}
