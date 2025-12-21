using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api_eCommerce.Endpoints
{
  public static class PublicTenantConfig
  {
    public static IEndpointRouteBuilder MapPublicTenantConfig(this IEndpointRouteBuilder app)
    {
      // Endpoint público para obtener configuración del tenant por slug
      app.MapGet("/api/public/tenant/{slug}", async (
          string slug,
          AdminDbContext adminDb,
          TenantDbContextFactory dbFactory,
          IConfiguration configuration) =>
      {
        // Buscar tenant por slug
        var tenant = await adminDb.Tenants
                  .Include(x => x.Plan)
                  .AsNoTracking()
                  .FirstOrDefaultAsync(x => x.Slug == slug.ToLower());

        if (tenant == null)
        {
          return Results.NotFound(new { error = "Tenant not found", slug });
        }

        if (tenant.Status != TenantStatus.Ready)
        {
          return Results.Problem(
                    statusCode: 503,
                    title: "Tenant not ready",
                    detail: $"Tenant '{slug}' is in status: {tenant.Status}"
                );
        }

        // Obtener features del plan
        var planFeatures = await adminDb.PlanFeatures
                  .Where(pf => pf.PlanId == tenant.PlanId)
                  .Include(pf => pf.Feature)
                  .AsNoTracking()
                  .ToListAsync();

        var features = planFeatures.ToDictionary(
                  pf => ToCamelCase(pf.Feature.Code),
                  pf => pf.Enabled
        );

        // Obtener settings del tenant (branding, locale, etc.)
        Dictionary<string, string> settings = new();
        try
        {
          // Construir connection string para el tenant
          var template = configuration["Tenancy:TenantDbTemplate"];
          if (!string.IsNullOrEmpty(template))
          {
            var connectionString = template.Replace("{dbname}", tenant.DbName);
            await using var tenantDb = dbFactory.Create(connectionString);

            settings = await tenantDb.Settings
                      .AsNoTracking()
                      .ToDictionaryAsync(s => s.Key, s => s.Value);
          }
        }
        catch
        {
          // Si falla obtener settings, continuar con defaults
        }

        // Construir respuesta
        var response = new PublicTenantConfigResponse
        {
          Tenant = new TenantInfo
          {
            Id = tenant.Id,
            Slug = tenant.Slug,
            DisplayName = tenant.Name,
            Status = tenant.Status.ToString(),
            Plan = tenant.Plan?.Code ?? "free",
            Branding = new BrandingInfo
            {
              LogoUrl = settings.GetValueOrDefault("LogoUrl"),
              FaviconUrl = settings.GetValueOrDefault("FaviconUrl"),
              PrimaryColor = settings.GetValueOrDefault("PrimaryColor", "#3b82f6"),
              SecondaryColor = settings.GetValueOrDefault("SecondaryColor", "#1e40af"),
              AccentColor = settings.GetValueOrDefault("AccentColor", "#10b981"),
              BackgroundColor = settings.GetValueOrDefault("BackgroundColor", "#ffffff")
            }
          },
          Locale = settings.GetValueOrDefault("Locale", "es-CO"),
          Currency = settings.GetValueOrDefault("Currency", "COP"),
          CurrencySymbol = settings.GetValueOrDefault("CurrencySymbol", "$"),
          TaxRate = decimal.TryParse(settings.GetValueOrDefault("TaxRate", "0"), out var tax) ? tax : 0m,
          Theme = new ThemeInfo
          {
            Primary = settings.GetValueOrDefault("PrimaryColor", "#3b82f6"),
            Accent = settings.GetValueOrDefault("AccentColor", "#10b981"),
            Background = settings.GetValueOrDefault("BackgroundColor", "#ffffff"),
            LogoUrl = settings.GetValueOrDefault("LogoUrl"),
            FaviconUrl = settings.GetValueOrDefault("FaviconUrl")
          },
          Features = features,
          Contact = new ContactInfo
          {
            Email = settings.GetValueOrDefault("ContactEmail"),
            Phone = settings.GetValueOrDefault("ContactPhone"),
            Address = settings.GetValueOrDefault("ContactAddress"),
            WhatsApp = settings.GetValueOrDefault("WhatsAppNumber")
          },
          Social = new SocialInfo
          {
            Facebook = settings.GetValueOrDefault("FacebookUrl"),
            Instagram = settings.GetValueOrDefault("InstagramUrl"),
            Twitter = settings.GetValueOrDefault("TwitterUrl"),
            TikTok = settings.GetValueOrDefault("TikTokUrl")
          },
          Seo = new SeoInfo
          {
            Title = settings.GetValueOrDefault("SeoTitle", tenant.Name),
            Description = settings.GetValueOrDefault("SeoDescription"),
            Keywords = settings.GetValueOrDefault("SeoKeywords")
          },
          AppFeatures = new AppFeaturesInfo
          {
            EnableCart = settings.GetValueOrDefault("EnableCart", "true") == "true",
            EnableWishlist = settings.GetValueOrDefault("EnableWishlist", "true") == "true",
            EnableReviews = settings.GetValueOrDefault("EnableReviews", "false") == "true",
            EnableLoyalty = settings.GetValueOrDefault("EnableLoyalty", "true") == "true",
            EnableGuestCheckout = settings.GetValueOrDefault("EnableGuestCheckout", "true") == "true",
            EnableNotifications = settings.GetValueOrDefault("EnableNotifications", "true") == "true"
          },
          Messages = new MessagesInfo
          {
            Welcome = settings.GetValueOrDefault("WelcomeMessage", "¡Bienvenido!"),
            CartEmpty = settings.GetValueOrDefault("CartEmptyMessage", "Tu carrito está vacío"),
            CheckoutSuccess = settings.GetValueOrDefault("CheckoutSuccessMessage", "¡Gracias por tu compra!"),
            OutOfStock = settings.GetValueOrDefault("OutOfStockMessage", "Producto agotado")
          }
        };

        return Results.Ok(response);
      })
      .WithName("GetPublicTenantConfig")
      .WithTags("Public")
      .WithSummary("Get tenant public configuration by slug")
      .WithDescription("Returns tenant branding, features, locale and theme settings. No authentication required.")
      .Produces<PublicTenantConfigResponse>(StatusCodes.Status200OK)
      .Produces(StatusCodes.Status404NotFound)
      .Produces(StatusCodes.Status503ServiceUnavailable);

      // Mantener el endpoint legacy por compatibilidad
      app.MapGet("/public/tenant-config", async (HttpContext http, AdminDbContext adminDb, ITenantResolver resolver) =>
      {
        var ctx = await resolver.ResolveAsync(http);
        if (ctx == null) return Results.Problem(statusCode: 409, detail: "Tenant not resolved or not ready");
        var t = await adminDb.Tenants.Include(x => x.Plan).AsNoTracking().FirstAsync(x => x.Slug == ctx.Slug);
        var features = await adminDb.PlanFeatures.Where(pf => pf.PlanId == t.PlanId).Select(pf => pf.Feature.Code).ToListAsync();
        return Results.Ok(new { name = t.Name, slug = t.Slug, theme = new { }, seo = new { }, features });
      })
      .WithTags("Public")
      .ExcludeFromDescription(); // Ocultar del swagger, es legacy

      return app;
    }

    private static string ToCamelCase(string str)
    {
      if (string.IsNullOrEmpty(str)) return str;
      return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
  }

  #region Response DTOs

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

  public record SeoInfo
  {
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }
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

  #endregion
}