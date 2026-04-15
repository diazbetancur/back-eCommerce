using CC.Domain.Assets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace CC.Infraestructure.Services.Assets;

public sealed class ImageOptimizationService : IImageOptimizationService
{
  private readonly TenantAssetsOptions _options;
  private readonly ILogger<ImageOptimizationService> _logger;

  public ImageOptimizationService(
      IOptions<TenantAssetsOptions> options,
      ILogger<ImageOptimizationService> logger)
  {
    _options = options.Value;
    _logger = logger;
  }

  public async Task<OptimizedImagePayload?> TryOptimizeAsync(ImageOptimizationInput input, CancellationToken ct = default)
  {
    if (!_options.ImageOptimization.Enabled)
    {
      return null;
    }

    if (input.SizeBytes < _options.ImageOptimization.MinBytesToOptimize)
    {
      return null;
    }

    if (input.SizeBytes <= 0 || input.SizeBytes > _options.ImageOptimization.MaxInputBytes)
    {
      return null;
    }

    var extension = NormalizeExtension(input.Extension);
    var encoder = ResolveEncoder(extension, _options.ImageOptimization.JpegQuality);
    if (encoder == null)
    {
      return null;
    }

    try
    {
      if (input.Content.CanSeek)
      {
        input.Content.Position = 0;
      }

      await using var sourceBuffer = new MemoryStream();
      await input.Content.CopyToAsync(sourceBuffer, ct);
      sourceBuffer.Position = 0;

      using var image = await Image.LoadAsync(sourceBuffer, ct);
      StripMetadata(image);

      var optimizedBuffer = new MemoryStream();
      await image.SaveAsync(optimizedBuffer, encoder, ct);

      if (optimizedBuffer.Length <= 0 || optimizedBuffer.Length >= input.SizeBytes)
      {
        optimizedBuffer.Dispose();
        return null;
      }

      optimizedBuffer.Position = 0;

      return new OptimizedImagePayload
      {
        Content = optimizedBuffer,
        Extension = extension,
        ContentType = NormalizeContentType(extension, input.ContentType),
        SizeBytes = optimizedBuffer.Length
      };
    }
    catch (UnknownImageFormatException ex)
    {
      _logger.LogDebug(ex, "Image optimization skipped because format could not be parsed for {FileName}", input.OriginalFileName);
      return null;
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Image optimization failed for {FileName}. Upload will continue without optimization.", input.OriginalFileName);
      return null;
    }
    finally
    {
      if (input.Content.CanSeek)
      {
        input.Content.Position = 0;
      }
    }
  }

  private static void StripMetadata(Image image)
  {
    image.Metadata.ExifProfile = null;
    image.Metadata.IccProfile = null;
    image.Metadata.XmpProfile = null;
    image.Metadata.IptcProfile = null;
  }

  private static IImageEncoder? ResolveEncoder(string extension, int jpegQuality)
  {
    return extension switch
    {
      ".jpg" or ".jpeg" => new JpegEncoder
      {
        Quality = Math.Clamp(jpegQuality, 90, 100)
      },
      ".png" => new PngEncoder(),
      _ => null
    };
  }

  private static string NormalizeExtension(string extension)
  {
    var normalized = (extension ?? string.Empty).Trim().ToLowerInvariant();

    return normalized switch
    {
      ".jpg" or ".jpeg" => normalized,
      ".png" => normalized,
      _ => normalized
    };
  }

  private static string NormalizeContentType(string extension, string? contentType)
  {
    return extension switch
    {
      ".jpg" or ".jpeg" => "image/jpeg",
      ".png" => "image/png",
      _ => (contentType ?? string.Empty).Trim().ToLowerInvariant()
    };
  }
}
