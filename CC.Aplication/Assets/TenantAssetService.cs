using CC.Infraestructure.Tenant;
using CC.Domain.Assets;
using CC.Aplication.Plans;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CC.Aplication.Assets;

public sealed class TenantAssetService : IAssetService
{
  private readonly TenantDbContextFactory _dbFactory;
  private readonly IFileStorageProvider _storageProvider;
  private readonly IFileValidationService _validationService;
  private readonly IImageOptimizationService _imageOptimizationService;
  private readonly ITenantAssetQuotaService _quotaService;
  private readonly IPlanLimitService _planLimitService;
  private readonly TenantAssetsOptions _options;
  private readonly ILogger<TenantAssetService> _logger;

  public TenantAssetService(
      TenantDbContextFactory dbFactory,
      IFileStorageProvider storageProvider,
      IFileValidationService validationService,
      IImageOptimizationService imageOptimizationService,
      ITenantAssetQuotaService quotaService,
        IPlanLimitService planLimitService,
      IOptions<TenantAssetsOptions> options,
      ILogger<TenantAssetService> logger)
  {
    _dbFactory = dbFactory;
    _storageProvider = storageProvider;
    _validationService = validationService;
    _imageOptimizationService = imageOptimizationService;
    _quotaService = quotaService;
    _planLimitService = planLimitService;
    _options = options.Value;
    _logger = logger;
  }

  public async Task<TenantAssetDto> UploadAsync(UploadAssetCommand command, CancellationToken ct = default)
  {
    if (command.SizeBytes <= 0)
    {
      throw new InvalidOperationException("Empty file is not allowed.");
    }

    var maxSize = command.AssetType == TenantAssetType.Image ? _options.MaxImageBytes : _options.MaxVideoBytes;

    if (command.AssetType != TenantAssetType.Image && command.SizeBytes > maxSize)
    {
      throw new InvalidOperationException($"File exceeds max allowed size ({maxSize} bytes) for {command.AssetType}.");
    }

    if (command.AssetType == TenantAssetType.Image && command.SizeBytes > _options.ImageOptimization.MaxInputBytes)
    {
      throw new InvalidOperationException(
          $"Image exceeds max optimization input size ({_options.ImageOptimization.MaxInputBytes} bytes).");
    }

    var normalizedModule = NormalizeNullableKey(command.Module) ?? "general";
    var normalizedEntityType = NormalizeNullableKey(command.EntityType);
    var normalizedEntityId = NormalizeNullableKey(command.EntityId);

    await EnsureEntityLimitsAsync(command.TenantId, normalizedModule, normalizedEntityType, normalizedEntityId, command.AssetType, ct);

    var validation = await _validationService.ValidateAsync(new FileValidationInput
    {
      FileName = command.OriginalFileName,
      ContentType = command.ContentType,
      SizeBytes = command.SizeBytes,
      AssetType = command.AssetType,
      Content = command.Content
    }, ct);

    var uploadStream = command.Content;
    var uploadSizeBytes = command.SizeBytes;
    var uploadContentType = validation.ContentType;
    var uploadExtension = validation.Extension;
    OptimizedImagePayload? optimizedPayload = null;

    try
    {
      if (command.AssetType == TenantAssetType.Image)
      {
        optimizedPayload = await _imageOptimizationService.TryOptimizeAsync(new ImageOptimizationInput
        {
          OriginalFileName = command.OriginalFileName,
          Extension = validation.Extension,
          ContentType = validation.ContentType,
          SizeBytes = command.SizeBytes,
          Content = command.Content
        }, ct);

        if (optimizedPayload != null)
        {
          uploadStream = optimizedPayload.Content;
          uploadSizeBytes = optimizedPayload.SizeBytes;
          uploadContentType = optimizedPayload.ContentType;
          uploadExtension = optimizedPayload.Extension;
        }
      }

      if (uploadSizeBytes > maxSize)
      {
        throw new InvalidOperationException($"File exceeds max allowed size ({maxSize} bytes) for {command.AssetType}.");
      }

      await _quotaService.EnsureUploadAllowedAsync(command.TenantId, command.AssetType, uploadSizeBytes, ct);

      if (uploadStream.CanSeek)
      {
        uploadStream.Position = 0;
      }

      var key = TenantAssetHelpers.BuildStorageKey(
          command.TenantId,
        normalizedModule,
        normalizedEntityType,
        normalizedEntityId,
          command.AssetType,
          validation.SafeFileName,
          uploadExtension,
          _options.BasePrefix);

      var storageResult = await _storageProvider.UploadAsync(new StorageUploadRequest
      {
        StorageKey = key,
        ContentType = uploadContentType,
        Content = uploadStream,
        Visibility = command.Visibility
      }, ct);

      var publicUrl = ResolvePublicUrl(storageResult.Provider, storageResult.StorageKey, storageResult.PublicUrl, storageResult.UrlOrPath);

      await using var db = _dbFactory.Create();

      var asset = new TenantAsset
      {
        Id = Guid.NewGuid(),
        TenantId = command.TenantId,
        AssetType = command.AssetType,
        SourceType = TenantAssetSourceType.InternalStorage,
        Module = normalizedModule,
        EntityType = normalizedEntityType,
        EntityId = normalizedEntityId,
        OriginalFileName = command.OriginalFileName,
        SafeFileName = validation.SafeFileName,
        StorageKey = storageResult.StorageKey,
        StorageBucket = storageResult.StorageBucket,
        PublicUrl = publicUrl,
        UrlOrPath = publicUrl,
        SizeBytes = uploadSizeBytes,
        Extension = uploadExtension,
        ContentType = uploadContentType,
        Provider = storageResult.Provider,
        Visibility = command.Visibility,
        LifecycleStatus = TenantAssetLifecycleStatus.Active,
        PhysicalDeletionRequired = true,
        UploadedByUserId = command.UploadedByUserId,
        CreatedAt = DateTime.UtcNow
      };

      db.TenantAssets.Add(asset);

      await UpdateLegacyEntityImageFieldsAsync(db, asset, command.SetAsPrimary, ct);
      await db.SaveChangesAsync(ct);

      await _quotaService.IncreaseUsageAsync(command.TenantId, command.AssetType, uploadSizeBytes, ct);

      return MapToDto(asset);
    }
    finally
    {
      if (optimizedPayload?.Content is IAsyncDisposable asyncDisposable)
      {
        await asyncDisposable.DisposeAsync();
      }
      else
      {
        optimizedPayload?.Content?.Dispose();
      }
    }
  }

  public async Task<IReadOnlyList<TenantAssetDto>> ListByEntityAsync(Guid tenantId, string module, string entityType, string entityId, CancellationToken ct = default)
  {
    var normalizedModule = NormalizeNullableKey(module) ?? "general";
    var normalizedEntityType = NormalizeNullableKey(entityType) ?? string.Empty;
    var normalizedEntityId = NormalizeNullableKey(entityId) ?? string.Empty;

    await using var db = _dbFactory.Create();

    var assets = await db.TenantAssets
        .AsNoTracking()
        .Where(a => a.TenantId == tenantId &&
                    a.Module == normalizedModule &&
                    a.EntityType == normalizedEntityType &&
                    a.EntityId == normalizedEntityId &&
                    a.LifecycleStatus == TenantAssetLifecycleStatus.Active)
        .OrderByDescending(a => a.CreatedAt)
        .ToListAsync(ct);

    return assets.Select(a => MapToDto(a)).ToList();
  }

  public async Task SetPrimaryAsync(Guid tenantId, Guid assetId, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();

    var asset = await db.TenantAssets.FirstOrDefaultAsync(a =>
        a.TenantId == tenantId &&
        a.Id == assetId &&
        a.LifecycleStatus == TenantAssetLifecycleStatus.Active,
        ct);

    if (asset == null)
    {
      throw new InvalidOperationException("Asset not found.");
    }

    if (asset.AssetType != TenantAssetType.Image)
    {
      throw new InvalidOperationException("Only image assets can be set as primary.");
    }

    if (!asset.Module.Equals("product", StringComparison.OrdinalIgnoreCase) ||
        !Guid.TryParse(asset.EntityId, out var productId))
    {
      throw new InvalidOperationException("Set-primary is currently supported only for product assets.");
    }

    await UpsertProductImageCacheAsync(db, asset, setAsPrimary: true, ct);
    await db.SaveChangesAsync(ct);
  }

  public async Task DeleteSingleAsync(Guid tenantId, Guid assetId, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();

    var asset = await db.TenantAssets.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == assetId, ct);
    if (asset == null)
    {
      return;
    }

    var deleted = await DeleteInternalAssetAsync(db, asset, ct);
    await db.SaveChangesAsync(ct);

    if (!deleted)
    {
      throw new InvalidOperationException(
          $"Physical deletion failed for asset {assetId}. The file still exists in storage.");
    }
  }

  public async Task<int> PurgeByEntityAsync(Guid tenantId, string module, string entityType, string entityId, CancellationToken ct = default)
  {
    var normalizedModule = NormalizeNullableKey(module) ?? "general";
    var normalizedEntityType = NormalizeNullableKey(entityType) ?? string.Empty;
    var normalizedEntityId = NormalizeNullableKey(entityId) ?? string.Empty;

    await using var db = _dbFactory.Create();

    var assets = await db.TenantAssets
        .Where(a => a.TenantId == tenantId &&
                    a.Module == normalizedModule &&
                    a.EntityType == normalizedEntityType &&
                    a.EntityId == normalizedEntityId &&
                    a.LifecycleStatus != TenantAssetLifecycleStatus.PhysicallyDeleted)
        .ToListAsync(ct);

    var deleted = 0;
    foreach (var asset in assets)
    {
      if (await DeleteInternalAssetAsync(db, asset, ct))
      {
        deleted++;
      }
    }

    await db.SaveChangesAsync(ct);
    return deleted;
  }

  public async Task<int> PurgeByTenantAsync(Guid tenantId, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();

    var assets = await db.TenantAssets
        .Where(a => a.TenantId == tenantId && a.LifecycleStatus != TenantAssetLifecycleStatus.PhysicallyDeleted)
        .ToListAsync(ct);

    var deleted = 0;
    foreach (var asset in assets)
    {
      if (await DeleteInternalAssetAsync(db, asset, ct))
      {
        deleted++;
      }
    }

    await db.SaveChangesAsync(ct);
    return deleted;
  }

  public Task<TenantAssetQuotaStatusDto> GetQuotaStatusAsync(Guid tenantId, CancellationToken ct = default)
      => _quotaService.GetStatusAsync(tenantId, ct);

  public Task RecalculateQuotaAsync(Guid tenantId, CancellationToken ct = default)
      => _quotaService.RecalculateAsync(tenantId, ct);

  private async Task<bool> DeleteInternalAssetAsync(TenantDbContext db, TenantAsset asset, CancellationToken ct)
  {
    if (asset.SourceType == TenantAssetSourceType.ExternalUrl || string.IsNullOrWhiteSpace(asset.StorageKey))
    {
      asset.LifecycleStatus = TenantAssetLifecycleStatus.PhysicallyDeleted;
      asset.PhysicalDeletionExecuted = true;
      asset.PhysicalDeletionExecutedAt = DateTime.UtcNow;
      asset.DeletedAt = DateTime.UtcNow;
      await UpdateLegacyEntityAfterDeleteAsync(db, asset, ct);
      return true;
    }

    try
    {
      await _storageProvider.DeleteAsync(asset.StorageKey, ct);
      asset.LifecycleStatus = TenantAssetLifecycleStatus.PhysicallyDeleted;
      asset.PhysicalDeletionExecuted = true;
      asset.PhysicalDeletionExecutedAt = DateTime.UtcNow;
      asset.DeletedAt = DateTime.UtcNow;
      asset.PhysicalDeletionLastError = null;
      await UpdateLegacyEntityAfterDeleteAsync(db, asset, ct);

      await _quotaService.DecreaseUsageAsync(asset.TenantId, asset.AssetType, asset.SizeBytes, ct);
      return true;
    }
    catch (Exception ex)
    {
      asset.LifecycleStatus = TenantAssetLifecycleStatus.DeletionFailed;
      asset.PhysicalDeletionAttempts += 1;
      asset.PhysicalDeletionLastError = ex.Message;
      _logger.LogError(ex, "Error deleting tenant asset {AssetId} in provider", asset.Id);
      return false;
    }
  }

  private static async Task UpdateLegacyEntityImageFieldsAsync(TenantDbContext db, TenantAsset asset, bool setAsPrimary, CancellationToken ct)
  {
    if (asset.AssetType != TenantAssetType.Image)
    {
      return;
    }

    if (asset.Module.Equals("product", StringComparison.OrdinalIgnoreCase) &&
        Guid.TryParse(asset.EntityId, out var productId))
    {
      await UpsertProductImageCacheAsync(db, asset, setAsPrimary, ct);
    }

    if (asset.Module.Equals("category", StringComparison.OrdinalIgnoreCase) &&
        Guid.TryParse(asset.EntityId, out var categoryId))
    {
      var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, ct);
      if (category != null && (setAsPrimary || string.IsNullOrWhiteSpace(category.ImageUrl)))
      {
        category.ImageUrl = asset.StorageKey;
      }
    }

    if (asset.Module.Equals("banner", StringComparison.OrdinalIgnoreCase) &&
        Guid.TryParse(asset.EntityId, out var bannerId))
    {
      var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == bannerId, ct);
      if (banner != null && (setAsPrimary || string.IsNullOrWhiteSpace(banner.ImageUrl)))
      {
        banner.ImageUrl = asset.StorageKey ?? string.Empty;
      }
    }

    if ((asset.Module.Equals("loyalty", StringComparison.OrdinalIgnoreCase) ||
         asset.Module.Equals("loyalty-reward", StringComparison.OrdinalIgnoreCase)) &&
        Guid.TryParse(asset.EntityId, out var rewardId))
    {
      var reward = await db.LoyaltyRewards.FirstOrDefaultAsync(r => r.Id == rewardId, ct);
      if (reward != null && (setAsPrimary || string.IsNullOrWhiteSpace(reward.ImageUrl)))
      {
        reward.ImageUrl = asset.UrlOrPath;
      }
    }
  }

  private async Task EnsureEntityLimitsAsync(
      Guid tenantId,
      string module,
      string? entityType,
      string? entityId,
      TenantAssetType assetType,
      CancellationToken ct)
  {
    if (assetType != TenantAssetType.Image ||
        !module.Equals("product", StringComparison.OrdinalIgnoreCase) ||
        !Guid.TryParse(entityId, out var productId))
    {
      return;
    }

    var maxImagesPerProduct = await _planLimitService.GetLimitValueAsync(PlanLimitCodes.MaxProductImages);
    if (maxImagesPerProduct <= 0)
    {
      throw new InvalidOperationException("Your current plan does not allow product images.");
    }

    if (maxImagesPerProduct == -1)
    {
      return;
    }

    await using var db = _dbFactory.Create();

    var currentCount = await db.TenantAssets
        .CountAsync(a => a.TenantId == tenantId &&
                         a.AssetType == TenantAssetType.Image &&
                         a.Module == module &&
                         a.EntityId == productId.ToString().ToLowerInvariant() &&
                         a.LifecycleStatus == TenantAssetLifecycleStatus.Active,
            ct);

    if (currentCount >= maxImagesPerProduct)
    {
      throw new InvalidOperationException($"Image limit reached for this product ({maxImagesPerProduct}).");
    }
  }

  private static async Task UpsertProductImageCacheAsync(TenantDbContext db, TenantAsset asset, bool setAsPrimary, CancellationToken ct)
  {
    if (!Guid.TryParse(asset.EntityId, out var productId))
    {
      return;
    }

    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
    if (product == null)
    {
      return;
    }

    var existingImages = await db.ProductImages
        .Where(i => i.ProductId == productId)
        .OrderBy(i => i.Order)
        .ToListAsync(ct);

    var image = existingImages.FirstOrDefault(i => i.ImageUrl == asset.UrlOrPath);
    if (image == null)
    {
      image = new ProductImage
      {
        Id = Guid.NewGuid(),
        ProductId = productId,
        ImageUrl = asset.UrlOrPath,
        Order = existingImages.Count == 0 ? 0 : existingImages.Max(i => i.Order) + 1,
        IsPrimary = false
      };

      db.ProductImages.Add(image);
      existingImages.Add(image);
    }

    if (setAsPrimary || !existingImages.Any(i => i.IsPrimary))
    {
      foreach (var existing in existingImages)
      {
        existing.IsPrimary = false;
      }

      image.IsPrimary = true;
    }

    var primary = existingImages.FirstOrDefault(i => i.IsPrimary) ?? image;
    product.MainImageUrl = primary.ImageUrl;
  }

  private static async Task UpdateLegacyEntityAfterDeleteAsync(TenantDbContext db, TenantAsset asset, CancellationToken ct)
  {
    if (!asset.Module.Equals("product", StringComparison.OrdinalIgnoreCase) ||
        !Guid.TryParse(asset.EntityId, out var productId))
    {
      return;
    }

    var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
    if (product == null)
    {
      return;
    }

    var removed = await db.ProductImages
        .Where(i => i.ProductId == productId && i.ImageUrl == asset.UrlOrPath)
        .ToListAsync(ct);

    if (removed.Count > 0)
    {
      db.ProductImages.RemoveRange(removed);
    }

    var remaining = await db.ProductImages
        .Where(i => i.ProductId == productId)
        .OrderBy(i => i.Order)
        .ToListAsync(ct);

    if (remaining.Count == 0)
    {
      product.MainImageUrl = null;
      return;
    }

    if (!remaining.Any(i => i.IsPrimary))
    {
      remaining[0].IsPrimary = true;
    }

    product.MainImageUrl = remaining.First(i => i.IsPrimary).ImageUrl;
  }

  private static string? NormalizeNullableKey(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    return value.Trim().ToLowerInvariant();
  }

  private TenantAssetDto MapToDto(TenantAsset asset)
  {
    var publicUrl = ResolvePublicUrl(asset.Provider, asset.StorageKey, asset.PublicUrl, asset.UrlOrPath);

    return new TenantAssetDto
    {
      Id = asset.Id,
      TenantId = asset.TenantId,
      Module = asset.Module,
      EntityType = asset.EntityType,
      EntityId = asset.EntityId,
      StorageKey = asset.StorageKey,
      StorageBucket = asset.StorageBucket,
      PublicUrl = publicUrl,
      UrlOrPath = publicUrl,
      Provider = asset.Provider,
      AssetType = asset.AssetType,
      SizeBytes = asset.SizeBytes,
      ContentType = asset.ContentType,
      Extension = asset.Extension,
      LifecycleStatus = asset.LifecycleStatus,
      CreatedAt = asset.CreatedAt
    };
  }

  private string ResolvePublicUrl(string? provider, string? storageKey, string? publicUrl, string? fallbackUrlOrPath)
  {
    if (!string.IsNullOrWhiteSpace(storageKey) &&
        provider != null &&
        provider.Equals("CloudflareR2", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(_options.CloudflareR2.PublicBaseUrl))
    {
      return $"{_options.CloudflareR2.PublicBaseUrl.TrimEnd('/')}/{storageKey.TrimStart('/')}";
    }

    if (!string.IsNullOrWhiteSpace(publicUrl))
    {
      return publicUrl;
    }

    return fallbackUrlOrPath ?? string.Empty;
  }
}
