using System.Text;
using CC.Domain.Assets;
using Microsoft.Extensions.Options;

namespace CC.Infraestructure.Services.Assets;

public sealed class FileValidationService : IFileValidationService
{
  private readonly TenantAssetsOptions _options;

  public FileValidationService(IOptions<TenantAssetsOptions> options)
  {
    _options = options.Value;
  }

  public async Task<FileValidationResult> ValidateAsync(FileValidationInput input, CancellationToken ct = default)
  {
    var extension = Path.GetExtension(input.FileName)?.ToLowerInvariant() ?? string.Empty;
    var allowedExtensions = input.AssetType == TenantAssetType.Image
        ? _options.AllowedImageExtensions
        : _options.AllowedVideoExtensions;

    var allowedContentTypes = input.AssetType == TenantAssetType.Image
        ? _options.AllowedImageContentTypes
        : _options.AllowedVideoContentTypes;

    if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException($"Extension '{extension}' is not allowed for {input.AssetType}.");
    }

    var normalizedContentType = NormalizeContentType(input.ContentType, extension);

    if (!allowedContentTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException($"Content-Type '{input.ContentType}' is not allowed for {input.AssetType}.");
    }

    if (input.Content.CanSeek)
    {
      input.Content.Position = 0;
    }

    var signatureOk = await ValidateFileSignatureAsync(input.AssetType, extension, input.Content, ct);
    if (!signatureOk)
    {
      throw new InvalidOperationException("File signature does not match the declared type.");
    }

    if (input.Content.CanSeek)
    {
      input.Content.Position = 0;
    }

    return new FileValidationResult
    {
      SafeFileName = SanitizeFileName(input.FileName),
      Extension = extension,
      ContentType = normalizedContentType
    };
  }

  private static string NormalizeContentType(string? contentType, string extension)
  {
    if (!string.IsNullOrWhiteSpace(contentType))
    {
      var trimmed = contentType.Trim().ToLowerInvariant();
      if (trimmed != "application/octet-stream")
      {
        return trimmed;
      }
    }

    return extension switch
    {
      ".jpg" or ".jpeg" => "image/jpeg",
      ".png" => "image/png",
      ".webp" => "image/webp",
      ".mp4" => "video/mp4",
      ".webm" => "video/webm",
      _ => (contentType ?? string.Empty).Trim().ToLowerInvariant()
    };
  }

  private static async Task<bool> ValidateFileSignatureAsync(TenantAssetType assetType, string extension, Stream stream, CancellationToken ct)
  {
    var header = new byte[64];
    var read = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
    if (read <= 0)
    {
      return false;
    }

    return assetType switch
    {
      TenantAssetType.Image => ValidateImageSignature(extension, header, read),
      TenantAssetType.Video => ValidateVideoSignature(extension, header, read),
      _ => false
    };
  }

  private static bool ValidateImageSignature(string extension, byte[] header, int read)
  {
    return extension switch
    {
      ".jpg" or ".jpeg" => read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
      ".png" => read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
      ".webp" => read >= 12 && Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
      _ => false
    };
  }

  private static bool ValidateVideoSignature(string extension, byte[] header, int read)
  {
    return extension switch
    {
      ".webm" => read >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3,
      ".mp4" => read >= 12 && Encoding.ASCII.GetString(header, 4, 4) == "ftyp",
      _ => false
    };
  }

  private static string SanitizeFileName(string fileName)
  {
    var raw = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
    if (string.IsNullOrWhiteSpace(raw))
    {
      return "file";
    }

    var lower = raw.ToLowerInvariant();
    var sanitized = new StringBuilder(lower.Length);
    foreach (var c in lower)
    {
      sanitized.Append(char.IsLetterOrDigit(c) ? c : '-');
    }

    var compact = sanitized.ToString().Trim('-');
    while (compact.Contains("--", StringComparison.Ordinal))
    {
      compact = compact.Replace("--", "-", StringComparison.Ordinal);
    }

    return string.IsNullOrWhiteSpace(compact) ? "file" : compact;
  }
}
