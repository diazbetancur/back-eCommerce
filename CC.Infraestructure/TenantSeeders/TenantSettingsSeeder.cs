using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using CC.Domain.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CC.Infraestructure.TenantSeeders
{
  public static class TenantSettingsSeeder
  {
    private static Dictionary<string, string> GetDefaultSettings(string tenantSlug, string tenantName)
    {
      return new Dictionary<string, string>
      {
        // ==================== INFORMACIÓN BÁSICA ====================
        ["StoreName"] = tenantName,
        ["StoreSlug"] = tenantSlug,
        ["StoreDescription"] = $"Bienvenido a {tenantName}",

        // ==================== LOCALE & MONEDA ====================
        ["Locale"] = "es-CO",
        ["Currency"] = "COP",
        ["CurrencySymbol"] = "$",
        ["TaxRate"] = "19", // IVA Colombia
        ["Timezone"] = "America/Bogota",

        // ==================== BRANDING ====================
        ["LogoUrl"] = "", // El admin lo configura después
        ["FaviconUrl"] = "",
        ["PrimaryColor"] = "#3b82f6",    // Tailwind blue-500
        ["SecondaryColor"] = "#1e40af",  // Tailwind blue-800
        ["AccentColor"] = "#10b981",     // Tailwind emerald-500
        ["BackgroundColor"] = "#ffffff",
        ["TextColor"] = "#1f2937",       // Tailwind gray-800

        // ==================== CONTACTO ====================
        ["ContactEmail"] = $"contacto@{tenantSlug}.com",
        ["ContactPhone"] = "",
        ["ContactAddress"] = "",
        ["WhatsAppNumber"] = "",

        // ==================== REDES SOCIALES ====================
        ["FacebookUrl"] = "",
        ["InstagramUrl"] = "",
        ["TwitterUrl"] = "",
        ["TikTokUrl"] = "",
        ["YouTubeUrl"] = "",

        // ==================== SEO ====================
        ["SeoTitle"] = tenantName,
        ["SeoDescription"] = $"Tienda online {tenantName} - Los mejores productos al mejor precio",
        ["SeoKeywords"] = "tienda,online,productos,compras",
        ["SeoOgImage"] = "",

        // ==================== FEATURES HABILITADAS ====================
        ["EnableCart"] = "true",
        ["EnableWishlist"] = "true",
        ["EnableReviews"] = "false",
        ["EnableLoyalty"] = "true",
        ["EnableGuestCheckout"] = "true",
        ["EnableNotifications"] = "true",

        // ==================== LOYALTY / PUNTOS ====================
        ["LoyaltyEnabled"] = "true",
        ["LoyaltyPointsPerDollar"] = "1",
        ["LoyaltyMinRedemption"] = "100",
        ["LoyaltyPointValue"] = "0.01", // 100 puntos = $1

        // ==================== CHECKOUT ====================
        ["MinOrderAmount"] = "0",
        ["MaxOrderAmount"] = "10000000",
        ["RequireAddressForCheckout"] = "true",
        ["RequirePhoneForCheckout"] = "true",

        // ==================== HORARIO DE ATENCIÓN ====================
        ["BusinessHoursEnabled"] = "false",
        ["BusinessHoursStart"] = "08:00",
        ["BusinessHoursEnd"] = "18:00",
        ["BusinessDays"] = "1,2,3,4,5", // Lunes a Viernes

        // ==================== MENSAJES PERSONALIZADOS ====================
        ["WelcomeMessage"] = $"¡Bienvenido a {tenantName}!",
        ["CartEmptyMessage"] = "Tu carrito está vacío. ¡Descubre nuestros productos!",
        ["CheckoutSuccessMessage"] = "¡Gracias por tu compra! Recibirás un correo con los detalles.",
        ["OutOfStockMessage"] = "Producto temporalmente agotado",
      };
    }

    /// <summary>
    /// Seed de configuración para un tenant nuevo.
    /// Solo agrega settings que no existen (idempotente).
    /// </summary>
    public static async Task SeedAsync(
        TenantDbContext db,
        string tenantSlug,
        string tenantName,
        ILogger? logger = null)
    {
      logger?.LogInformation("🔧 Seeding tenant settings for {TenantSlug}...", tenantSlug);

      var existingKeys = (await db.Settings
          .Select(s => s.Key)
          .ToListAsync())
          .ToHashSet();

      var defaultSettings = GetDefaultSettings(tenantSlug, tenantName);
      var newSettings = new List<TenantSetting>();

      foreach (var (key, value) in defaultSettings)
      {
        if (!existingKeys.Contains(key))
        {
          newSettings.Add(new TenantSetting { Key = key, Value = value });
        }
      }

      if (newSettings.Any())
      {
        db.Settings.AddRange(newSettings);
        await db.SaveChangesAsync();
        logger?.LogInformation("✅ Added {Count} default settings for tenant {TenantSlug}",
            newSettings.Count, tenantSlug);
      }
      else
      {
        logger?.LogInformation("📦 All settings already exist for tenant {TenantSlug}", tenantSlug);
      }
    }

    public static async Task<TenantConfiguration> GetConfigurationAsync(TenantDbContext db)
    {
      var settings = await db.Settings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);

      return new TenantConfiguration
      {
        StoreName = settings.GetValueOrDefault("StoreName", "Mi Tienda"),
        StoreDescription = settings.GetValueOrDefault("StoreDescription", ""),

        Locale = new LocaleConfig
        {
          Locale = settings.GetValueOrDefault("Locale", "es-CO"),
          Currency = settings.GetValueOrDefault("Currency", "COP"),
          CurrencySymbol = settings.GetValueOrDefault("CurrencySymbol", "$"),
          TaxRate = decimal.TryParse(settings.GetValueOrDefault("TaxRate", "19"), out var tax) ? tax : 19m,
          Timezone = settings.GetValueOrDefault("Timezone", "America/Bogota")
        },

        Branding = new BrandingConfig
        {
          LogoUrl = settings.GetValueOrDefault("LogoUrl"),
          FaviconUrl = settings.GetValueOrDefault("FaviconUrl"),
          PrimaryColor = settings.GetValueOrDefault("PrimaryColor", "#3b82f6"),
          SecondaryColor = settings.GetValueOrDefault("SecondaryColor", "#1e40af"),
          AccentColor = settings.GetValueOrDefault("AccentColor", "#10b981"),
          BackgroundColor = settings.GetValueOrDefault("BackgroundColor", "#ffffff"),
          TextColor = settings.GetValueOrDefault("TextColor", "#1f2937")
        },

        Contact = new ContactConfig
        {
          Email = settings.GetValueOrDefault("ContactEmail"),
          Phone = settings.GetValueOrDefault("ContactPhone"),
          Address = settings.GetValueOrDefault("ContactAddress"),
          WhatsApp = settings.GetValueOrDefault("WhatsAppNumber")
        },

        Social = new SocialConfig
        {
          Facebook = settings.GetValueOrDefault("FacebookUrl"),
          Instagram = settings.GetValueOrDefault("InstagramUrl"),
          Twitter = settings.GetValueOrDefault("TwitterUrl"),
          TikTok = settings.GetValueOrDefault("TikTokUrl"),
          YouTube = settings.GetValueOrDefault("YouTubeUrl")
        },

        Seo = new SeoConfig
        {
          Title = settings.GetValueOrDefault("SeoTitle"),
          Description = settings.GetValueOrDefault("SeoDescription"),
          Keywords = settings.GetValueOrDefault("SeoKeywords"),
          OgImage = settings.GetValueOrDefault("SeoOgImage")
        },

        Features = new FeaturesConfig
        {
          EnableCart = settings.GetValueOrDefault("EnableCart", "true") == "true",
          EnableWishlist = settings.GetValueOrDefault("EnableWishlist", "true") == "true",
          EnableReviews = settings.GetValueOrDefault("EnableReviews", "false") == "true",
          EnableLoyalty = settings.GetValueOrDefault("EnableLoyalty", "true") == "true",
          EnableGuestCheckout = settings.GetValueOrDefault("EnableGuestCheckout", "true") == "true",
          EnableNotifications = settings.GetValueOrDefault("EnableNotifications", "true") == "true"
        },

        Loyalty = new LoyaltyConfig
        {
          Enabled = settings.GetValueOrDefault("LoyaltyEnabled", "true") == "true",
          PointsPerDollar = int.TryParse(settings.GetValueOrDefault("LoyaltyPointsPerDollar", "1"), out var ppd) ? ppd : 1,
          MinRedemption = int.TryParse(settings.GetValueOrDefault("LoyaltyMinRedemption", "100"), out var mr) ? mr : 100,
          PointValue = decimal.TryParse(settings.GetValueOrDefault("LoyaltyPointValue", "0.01"), out var pv) ? pv : 0.01m
        },

        Messages = new MessagesConfig
        {
          Welcome = settings.GetValueOrDefault("WelcomeMessage", "¡Bienvenido!"),
          CartEmpty = settings.GetValueOrDefault("CartEmptyMessage", "Tu carrito está vacío"),
          CheckoutSuccess = settings.GetValueOrDefault("CheckoutSuccessMessage", "¡Gracias por tu compra!"),
          OutOfStock = settings.GetValueOrDefault("OutOfStockMessage", "Producto agotado")
        }
      };
    }
  }
}
