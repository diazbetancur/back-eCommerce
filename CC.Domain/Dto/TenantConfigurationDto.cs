namespace CC.Domain.Dto
{
  /// <summary>
  /// DTOs para configuración del tenant
  /// </summary>
  public class TenantConfiguration
  {
    public string StoreName { get; set; } = "";
    public string StoreDescription { get; set; } = "";
    public LocaleConfig Locale { get; set; } = new();
    public BrandingConfig Branding { get; set; } = new();
    public ContactConfig Contact { get; set; } = new();
    public SocialConfig Social { get; set; } = new();
    public SeoConfig Seo { get; set; } = new();
    public FeaturesConfig Features { get; set; } = new();
    public LoyaltyConfig Loyalty { get; set; } = new();
    public MessagesConfig Messages { get; set; } = new();
  }

  public class LocaleConfig
  {
    public string Locale { get; set; } = "es-CO";
    public string Currency { get; set; } = "COP";
    public string CurrencySymbol { get; set; } = "$";
    public decimal TaxRate { get; set; } = 19;
    public string Timezone { get; set; } = "America/Bogota";
  }

  public class BrandingConfig
  {
    public string? LogoUrl { get; set; }
    public string? FaviconUrl { get; set; }
    public string PrimaryColor { get; set; } = "#3b82f6";
    public string SecondaryColor { get; set; } = "#1e40af";
    public string AccentColor { get; set; } = "#10b981";
    public string BackgroundColor { get; set; } = "#ffffff";
    public string TextColor { get; set; } = "#1f2937";
  }

  public class ContactConfig
  {
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? WhatsApp { get; set; }
  }

  public class SocialConfig
  {
    public string? Facebook { get; set; }
    public string? Instagram { get; set; }
    public string? Twitter { get; set; }
    public string? TikTok { get; set; }
    public string? YouTube { get; set; }
  }

  public class SeoConfig
  {
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public string? OgImage { get; set; }
  }

  public class FeaturesConfig
  {
    public bool EnableCart { get; set; } = true;
    public bool EnableWishlist { get; set; } = true;
    public bool EnableReviews { get; set; } = false;
    public bool EnableLoyalty { get; set; } = true;
    public bool EnableGuestCheckout { get; set; } = true;
    public bool EnableNotifications { get; set; } = true;
  }

  public class LoyaltyConfig
  {
    public bool Enabled { get; set; } = true;
    public int PointsPerDollar { get; set; } = 1;
    public int MinRedemption { get; set; } = 100;
    public decimal PointValue { get; set; } = 0.01m;
  }

  public class MessagesConfig
  {
    public string Welcome { get; set; } = "¡Bienvenido!";
    public string CartEmpty { get; set; } = "Tu carrito está vacío";
    public string CheckoutSuccess { get; set; } = "¡Gracias por tu compra!";
    public string OutOfStock { get; set; } = "Producto agotado";
  }
}
