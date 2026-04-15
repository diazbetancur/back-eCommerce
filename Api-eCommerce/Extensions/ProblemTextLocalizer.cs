using System.Text.RegularExpressions;

namespace Api_eCommerce.Extensions;

public static class ProblemTextLocalizer
{
  private static readonly Dictionary<string, string> ExactMap = new(StringComparer.OrdinalIgnoreCase)
  {
    ["Tenant Not Resolved"] = "Tenant no resuelto",
    ["Validation Error"] = "Error de validación",
    ["Internal Server Error"] = "Error interno del servidor",
    ["Not Found"] = "No encontrado",
    ["Product Not Found"] = "Producto no encontrado",
    ["Order Not Found"] = "Orden no encontrada",
    ["Favorite Not Found"] = "Favorito no encontrado",
    ["Resource Not Found"] = "Recurso no encontrado",
    ["Feature disabled"] = "Funcionalidad deshabilitada",
    ["Quota exceeded or not configured"] = "Cuota excedida o no configurada",
    ["Tenant not resolved"] = "Tenant no resuelto",
    ["Tenant not resolved or not ready"] = "El tenant no está resuelto o no está listo.",
    ["Unable to resolve tenant from request"] = "No se pudo resolver el tenant para la solicitud.",
    ["User ID not found in token"] = "No se encontró el identificador de usuario en el token.",
    ["Title is required"] = "El título es obligatorio",
    ["Image is required"] = "La imagen es obligatoria",
    ["File is required"] = "El archivo es obligatorio",
    ["module is required"] = "El módulo es obligatorio",
    ["entityType is required"] = "El tipo de entidad es obligatorio",
    ["entityId is required"] = "El identificador de entidad es obligatorio",
    ["Invalid branding update request"] = "Solicitud de actualización de branding inválida",
    ["Invalid asset upload request"] = "Solicitud de carga de archivo inválida",
    ["Invalid set-primary request"] = "Solicitud inválida para establecer imagen principal",
    ["Could not resolve uploaded branding image URL."] = "No se pudo resolver la URL pública de la imagen cargada.",
    ["Banner image is required"] = "La imagen del banner es obligatoria",
    ["Popup image is required"] = "La imagen del popup es obligatoria",
    ["Category not found"] = "Categoría no encontrada",
    ["Product not found"] = "Producto no encontrado",
    ["Banner not found"] = "Banner no encontrado",
    ["Popup not found"] = "Popup no encontrado"
  };

  public static string? ToSpanish(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return text;
    }

    var value = text.Trim();

    if (ExactMap.TryGetValue(value, out var exact))
    {
      return exact;
    }

    var productNotFound = Regex.Match(value, @"^Product\s+(?<id>.+)\s+not found$", RegexOptions.IgnoreCase);
    if (productNotFound.Success)
    {
      return $"Producto {productNotFound.Groups["id"].Value} no encontrado";
    }

    var roleNotFound = Regex.Match(value, @"^Role with ID\s+(?<id>.+)\s+not found\.?$", RegexOptions.IgnoreCase);
    if (roleNotFound.Success)
    {
      return $"Rol con ID {roleNotFound.Groups["id"].Value} no encontrado";
    }

    var userNotFound = Regex.Match(value, @"^User with id\s+'(?<id>.+)'\s+not found$", RegexOptions.IgnoreCase);
    if (userNotFound.Success)
    {
      return $"Usuario con id '{userNotFound.Groups["id"].Value}' no encontrado";
    }

    if (value.Contains("not found", StringComparison.OrdinalIgnoreCase))
    {
      return "No se encontró el recurso solicitado.";
    }

    if (value.Contains("already exists", StringComparison.OrdinalIgnoreCase))
    {
      return "El recurso ya existe.";
    }

    if (value.Contains("is required", StringComparison.OrdinalIgnoreCase))
    {
      return "Hay campos obligatorios sin completar.";
    }

    return text;
  }
}
