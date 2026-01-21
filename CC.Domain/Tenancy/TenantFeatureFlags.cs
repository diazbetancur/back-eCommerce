using System.Text.Json;

namespace CC.Domain.Tenancy
{
    /// <summary>
    /// Configuraci�n de feature flags por tenant
    /// </summary>
    public class TenantFeatureFlags
    {
        // Checkout Features
        public bool AllowGuestCheckout { get; set; } = true;
        public bool RequirePhoneNumber { get; set; } = false;
        public bool EnableExpressCheckout { get; set; } = false;

        // Catalog Features
        public bool ShowStock { get; set; } = true;
        public bool HasVariants { get; set; } = false;
        public bool EnableMultiStore { get; set; } = false;
        public bool EnableWishlist { get; set; } = false;
        public bool EnableReviews { get; set; } = false;

        // Payment Features
        public PaymentFeatures Payments { get; set; } = new();

        // Cart Features
        public bool EnableCartSave { get; set; } = false;
        public int MaxCartItems { get; set; } = 100;

        // Search & Filters
        public bool EnableAdvancedSearch { get; set; } = false;
        public bool EnableFilters { get; set; } = true;

        // Analytics & Marketing
        public bool EnableAnalytics { get; set; } = false;
        public bool EnableNewsletterSignup { get; set; } = false;

        /// <summary>
        /// Obtiene el valor de una feature espec�fica por path (ej: "payments.wompiEnabled")
        /// </summary>
        public T? GetValue<T>(string path, T? defaultValue = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(this);
                using var doc = JsonDocument.Parse(json);
                var element = doc.RootElement;

                foreach (var segment in path.Split('.'))
                {
                    if (!element.TryGetProperty(segment, out var nextElement))
                        return defaultValue;
                    element = nextElement;
                }

                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Verifica si una feature est� habilitada (asume que es booleana)
        /// </summary>
        public bool IsEnabled(string path)
        {
            return GetValue(path, false);
        }
    }

    public class PaymentFeatures
    {
        public bool WompiEnabled { get; set; } = false;
        public bool StripeEnabled { get; set; } = false;
        public bool PayPalEnabled { get; set; } = false;
        public bool CashOnDelivery { get; set; } = true;
    }

    /// <summary>
    /// Feature flags por defecto seg�n el plan
    /// </summary>
    public static class DefaultFeatureFlags
    {
        public static TenantFeatureFlags GetForPlan(string plan)
        {
            return plan?.ToUpper() switch
            {
                "BASIC" => new TenantFeatureFlags
                {
                    AllowGuestCheckout = true,
                    ShowStock = true,
                    HasVariants = false,
                    EnableMultiStore = false,
                    EnableWishlist = false,
                    EnableReviews = false,
                    EnableAdvancedSearch = false,
                    EnableAnalytics = false,
                    MaxCartItems = 50,
                    Payments = new PaymentFeatures
                    {
                        CashOnDelivery = true,
                        WompiEnabled = false,
                        StripeEnabled = false,
                        PayPalEnabled = false
                    }
                },
                "PREMIUM" => new TenantFeatureFlags
                {
                    AllowGuestCheckout = true,
                    ShowStock = true,
                    HasVariants = true,
                    EnableMultiStore = true,
                    EnableWishlist = true,
                    EnableReviews = true,
                    EnableAdvancedSearch = true,
                    EnableAnalytics = false,
                    MaxCartItems = 100,
                    Payments = new PaymentFeatures
                    {
                        CashOnDelivery = true,
                        WompiEnabled = true,
                        StripeEnabled = false,
                        PayPalEnabled = false
                    }
                },
                "ENTERPRISE" => new TenantFeatureFlags
                {
                    AllowGuestCheckout = true,
                    ShowStock = true,
                    HasVariants = true,
                    EnableMultiStore = true,
                    EnableWishlist = true,
                    EnableReviews = true,
                    EnableAdvancedSearch = true,
                    EnableAnalytics = true,
                    EnableCartSave = true,
                    MaxCartItems = 200,
                    EnableExpressCheckout = true,
                    Payments = new PaymentFeatures
                    {
                        CashOnDelivery = true,
                        WompiEnabled = true,
                        StripeEnabled = true,
                        PayPalEnabled = true
                    }
                },
                _ => new TenantFeatureFlags() // Default b�sico
            };
        }
    }
}
