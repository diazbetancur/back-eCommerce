namespace CC.Domain.Features
{
    /// <summary>
    /// Constantes para las claves de feature flags
    /// </summary>
    public static class FeatureKeys
    {
        // Checkout Features
        public const string AllowGuestCheckout = "allowGuestCheckout";
        public const string RequirePhoneNumber = "requirePhoneNumber";
        public const string EnableExpressCheckout = "enableExpressCheckout";

        // Catalog Features
        public const string ShowStock = "showStock";
        public const string HasVariants = "hasVariants";
        public const string EnableWishlist = "enableWishlist";
        public const string EnableReviews = "enableReviews";

        // Payment Features
        public const string PaymentsWompiEnabled = "payments.wompiEnabled";
        public const string PaymentsStripeEnabled = "payments.stripeEnabled";
        public const string PaymentsPayPalEnabled = "payments.payPalEnabled";
        public const string PaymentsCashOnDelivery = "payments.cashOnDelivery";

        // Cart Features
        public const string EnableCartSave = "enableCartSave";
        public const string MaxCartItems = "maxCartItems";

        // Search & Filters
        public const string EnableAdvancedSearch = "enableAdvancedSearch";
        public const string EnableFilters = "enableFilters";

        // Analytics & Marketing
        public const string EnableAnalytics = "enableAnalytics";
        public const string EnableNewsletterSignup = "enableNewsletterSignup";
    }
}
