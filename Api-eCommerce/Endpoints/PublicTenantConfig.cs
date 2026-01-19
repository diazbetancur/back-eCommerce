using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Domain.Dto;
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
          Tenant = new CC.Domain.Dto.TenantInfo
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
}
