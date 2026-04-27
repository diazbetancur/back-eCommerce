using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace Api_eCommerce.ModelBinding;

public sealed class NullableDecimalFormValueBinder : IModelBinder
{
  public Task BindModelAsync(ModelBindingContext bindingContext)
  {
    ArgumentNullException.ThrowIfNull(bindingContext);

    var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
    if (valueResult == ValueProviderResult.None)
    {
      bindingContext.Result = ModelBindingResult.Success(null);
      return Task.CompletedTask;
    }

    bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

    var rawValue = valueResult.FirstValue;
    if (string.IsNullOrWhiteSpace(rawValue)
        || string.Equals(rawValue, "null", StringComparison.OrdinalIgnoreCase))
    {
      bindingContext.Result = ModelBindingResult.Success(null);
      return Task.CompletedTask;
    }

    if (TryParse(rawValue, out var parsedValue))
    {
      bindingContext.Result = ModelBindingResult.Success(parsedValue);
      return Task.CompletedTask;
    }

    var fieldName = bindingContext.ModelMetadata.DisplayName
        ?? bindingContext.FieldName
        ?? bindingContext.ModelName;

    bindingContext.ModelState.TryAddModelError(
        bindingContext.ModelName,
        $"The value '{rawValue}' is not valid for {fieldName}.");

    return Task.CompletedTask;
  }

  private static bool TryParse(string rawValue, out decimal parsedValue)
  {
    return decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
        || decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.CurrentCulture, out parsedValue);
  }
}