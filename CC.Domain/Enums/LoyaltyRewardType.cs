namespace CC.Domain.Enums
{
  /// <summary>
  /// Tipos de premios disponibles en el programa de lealtad
  /// </summary>
  public static class LoyaltyRewardType
  {
    /// <summary>
    /// Producto físico del catálogo
    /// </summary>
    public const string Product = "PRODUCT";

    /// <summary>
    /// Descuento porcentual (ej: 10% de descuento)
    /// </summary>
    public const string DiscountPercentage = "DISCOUNT_PERCENTAGE";

    /// <summary>
    /// Descuento de monto fijo (ej: $5000 de descuento)
    /// </summary>
    public const string DiscountFixed = "DISCOUNT_FIXED";

    /// <summary>
    /// Envío gratis en la próxima compra
    /// </summary>
    public const string FreeShipping = "FREE_SHIPPING";

    /// <summary>
    /// Validar si el tipo es válido
    /// </summary>
    public static bool IsValid(string type)
    {
      return type == Product ||
             type == DiscountPercentage ||
             type == DiscountFixed ||
             type == FreeShipping;
    }
  }
}
