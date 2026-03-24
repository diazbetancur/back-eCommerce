using System.Text;
using System.Text.RegularExpressions;
using CC.Domain.Assets;

namespace CC.Aplication.Assets;

internal static class TenantAssetHelpers
{
  public static string SanitizeFileName(string fileName)
  {
    var clean = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
    if (string.IsNullOrWhiteSpace(clean))
    {
      clean = "file";
    }

    clean = RemoveDiacritics(clean).ToLowerInvariant();
    clean = Regex.Replace(clean, "[^a-z0-9-]+", "-");
    clean = Regex.Replace(clean, "-+", "-").Trim('-');

    return string.IsNullOrWhiteSpace(clean) ? "file" : clean;
  }

  public static string BuildStorageKey(Guid tenantId, string module, string? entityType, string? entityId, TenantAssetType assetType, string safeName, string extension, string basePrefix)
  {
    var normalizedModule = string.IsNullOrWhiteSpace(module) ? "general" : module.Trim().ToLowerInvariant();
    var normalizedEntityType = string.IsNullOrWhiteSpace(entityType) ? "unbound" : entityType.Trim().ToLowerInvariant();
    var normalizedEntityId = string.IsNullOrWhiteSpace(entityId) ? "unassigned" : entityId.Trim().ToLowerInvariant();
    var kind = assetType == TenantAssetType.Image ? "images" : "videos";
    var now = DateTime.UtcNow;
    var unique = Guid.NewGuid().ToString("N");

    return $"{basePrefix}/{tenantId}/{normalizedModule}/{normalizedEntityType}/{normalizedEntityId}/{kind}/{now:yyyy}/{now:MM}/{unique}_{safeName}{extension}";
  }

  private static string RemoveDiacritics(string text)
  {
    var normalized = text.Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder();

    foreach (var c in normalized)
    {
      var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
      if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
      {
        sb.Append(c);
      }
    }

    return sb.ToString().Normalize(NormalizationForm.FormC);
  }
}
