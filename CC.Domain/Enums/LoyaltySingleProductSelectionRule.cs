namespace CC.Domain.Enums
{
  /// <summary>
  /// Regla para seleccionar un único producto elegible del carrito
  /// cuando el descuento no aplica sobre todos.
  /// </summary>
  public static class LoyaltySingleProductSelectionRule
  {
    public const string MostExpensive = "MOST_EXPENSIVE";
    public const string Cheapest = "CHEAPEST";

    public static bool IsValid(string? value)
    {
      return value == MostExpensive || value == Cheapest;
    }
  }
}
