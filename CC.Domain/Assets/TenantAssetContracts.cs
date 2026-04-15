namespace CC.Domain.Assets;

public enum TenantAssetType
{
  Image = 1,
  Video = 2
}

public enum TenantAssetSourceType
{
  ExternalUrl = 1,
  InternalStorage = 2
}

public enum TenantAssetVisibility
{
  Public = 1,
  Private = 2
}

public enum TenantAssetLifecycleStatus
{
  Active = 1,
  DeletionRequested = 2,
  PhysicallyDeleted = 3,
  DeletionFailed = 4
}

public sealed class TenantAssetsOptions
{
  public const string SectionName = "TenantAssets";

  public string ActiveProvider { get; set; } = "CloudflareR2";
  public string BasePrefix { get; set; } = "tenants";
  public long MaxImageBytes { get; set; } = 1_048_576;
  public long MaxVideoBytes { get; set; } = 5_242_880;
  public List<string> AllowedImageExtensions { get; set; } = new() { ".jpg", ".jpeg", ".png", ".webp" };
  public List<string> AllowedVideoExtensions { get; set; } = new() { ".mp4", ".webm" };
  public List<string> AllowedImageContentTypes { get; set; } = new() { "image/jpeg", "image/png", "image/webp" };
  public List<string> AllowedVideoContentTypes { get; set; } = new() { "video/mp4", "video/webm" };

  public TenantImageOptimizationOptions ImageOptimization { get; set; } = new();

  public CloudflareR2AssetsOptions CloudflareR2 { get; set; } = new();
}

public sealed class TenantImageOptimizationOptions
{
  public bool Enabled { get; set; } = true;
  public long MaxInputBytes { get; set; } = 5_242_880;
  public long MinBytesToOptimize { get; set; } = 122_880;
  public int JpegQuality { get; set; } = 100;
  public int WebpQuality { get; set; } = 100;
}

public sealed class CloudflareR2AssetsOptions
{
  public string AccountId { get; set; } = string.Empty;
  public string BucketName { get; set; } = string.Empty;
  public string Endpoint { get; set; } = string.Empty;
  public string AccessKey { get; set; } = string.Empty;
  public string SecretKey { get; set; } = string.Empty;
  public string PublicBaseUrl { get; set; } = string.Empty;
  public bool IsPublicBucket { get; set; } = true;
}

public sealed class UploadAssetCommand
{
  public Guid TenantId { get; init; }
  public string UploadedByUserId { get; init; } = string.Empty;
  public string Module { get; init; } = string.Empty;
  public string? EntityType { get; init; }
  public string? EntityId { get; init; }
  public TenantAssetType AssetType { get; init; }
  public TenantAssetVisibility Visibility { get; init; } = TenantAssetVisibility.Public;
  public required string OriginalFileName { get; init; }
  public required string ContentType { get; init; }
  public required long SizeBytes { get; init; }
  public required Stream Content { get; init; }
  public bool SetAsPrimary { get; init; }
}

public sealed class TenantAssetDto
{
  public Guid Id { get; init; }
  public Guid TenantId { get; init; }
  public string Module { get; init; } = string.Empty;
  public string? EntityType { get; init; }
  public string? EntityId { get; init; }
  public string? StorageKey { get; init; }
  public string? StorageBucket { get; init; }
  public string? PublicUrl { get; init; }
  public string UrlOrPath { get; init; } = string.Empty;
  public string Provider { get; init; } = string.Empty;
  public TenantAssetType AssetType { get; init; }
  public long SizeBytes { get; init; }
  public string ContentType { get; init; } = string.Empty;
  public string Extension { get; init; } = string.Empty;
  public TenantAssetLifecycleStatus LifecycleStatus { get; init; }
  public DateTime CreatedAt { get; init; }
}

public sealed class FileValidationInput
{
  public required string FileName { get; init; }
  public required string ContentType { get; init; }
  public required long SizeBytes { get; init; }
  public required TenantAssetType AssetType { get; init; }
  public required Stream Content { get; init; }
}

public sealed class FileValidationResult
{
  public required string SafeFileName { get; init; }
  public required string Extension { get; init; }
  public required string ContentType { get; init; }
}

public sealed class StorageUploadRequest
{
  public required string StorageKey { get; init; }
  public required string ContentType { get; init; }
  public required Stream Content { get; init; }
  public required TenantAssetVisibility Visibility { get; init; }
}

public sealed class StorageUploadResult
{
  public required string StorageKey { get; init; }
  public required string UrlOrPath { get; init; }
  public string? StorageBucket { get; init; }
  public string? PublicUrl { get; init; }
  public required string Provider { get; init; }
}

public sealed class ImageOptimizationInput
{
  public required string OriginalFileName { get; init; }
  public required string Extension { get; init; }
  public required string ContentType { get; init; }
  public required long SizeBytes { get; init; }
  public required Stream Content { get; init; }
}

public sealed class OptimizedImagePayload
{
  public required Stream Content { get; init; }
  public required string Extension { get; init; }
  public required string ContentType { get; init; }
  public required long SizeBytes { get; init; }
}

public sealed class TenantAssetQuotaStatusDto
{
  public Guid TenantId { get; init; }
  public int MaxImageCount { get; init; }
  public int MaxVideoCount { get; init; }
  public long MaxTotalBytes { get; init; }
  public bool AllowVideos { get; init; }
  public int CurrentImageCount { get; init; }
  public int CurrentVideoCount { get; init; }
  public long CurrentTotalBytes { get; init; }
  public DateTime LastRecalculatedAt { get; init; }
}

public interface IAssetService
{
  Task<TenantAssetDto> UploadAsync(UploadAssetCommand command, CancellationToken ct = default);
  Task<IReadOnlyList<TenantAssetDto>> ListByEntityAsync(Guid tenantId, string module, string entityType, string entityId, CancellationToken ct = default);
  Task SetPrimaryAsync(Guid tenantId, Guid assetId, CancellationToken ct = default);
  Task DeleteSingleAsync(Guid tenantId, Guid assetId, CancellationToken ct = default);
  Task<int> PurgeByEntityAsync(Guid tenantId, string module, string entityType, string entityId, CancellationToken ct = default);
  Task<int> PurgeByTenantAsync(Guid tenantId, CancellationToken ct = default);
  Task<TenantAssetQuotaStatusDto> GetQuotaStatusAsync(Guid tenantId, CancellationToken ct = default);
  Task RecalculateQuotaAsync(Guid tenantId, CancellationToken ct = default);
}

public interface IFileStorageProvider
{
  string ProviderName { get; }
  Task<StorageUploadResult> UploadAsync(StorageUploadRequest request, CancellationToken ct = default);
  Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

public interface IFileValidationService
{
  Task<FileValidationResult> ValidateAsync(FileValidationInput input, CancellationToken ct = default);
}

public interface IImageOptimizationService
{
  Task<OptimizedImagePayload?> TryOptimizeAsync(ImageOptimizationInput input, CancellationToken ct = default);
}

public sealed class PlanAssetLimits
{
  public string PlanCode { get; init; } = string.Empty;
  public int MaxImageCount { get; init; }
  public int MaxVideoCount { get; init; }
  public long MaxTotalBytes { get; init; }
  public bool AllowVideos { get; init; }
}

public interface ITenantPlanLimitResolver
{
  Task<PlanAssetLimits> ResolveAsync(CancellationToken ct = default);
}

public interface ITenantAssetQuotaService
{
  Task EnsureUploadAllowedAsync(Guid tenantId, TenantAssetType assetType, long bytesToAdd, CancellationToken ct = default);
  Task IncreaseUsageAsync(Guid tenantId, TenantAssetType assetType, long bytesToAdd, CancellationToken ct = default);
  Task DecreaseUsageAsync(Guid tenantId, TenantAssetType assetType, long bytesToRemove, CancellationToken ct = default);
  Task<TenantAssetQuotaStatusDto> GetStatusAsync(Guid tenantId, CancellationToken ct = default);
  Task RecalculateAsync(Guid tenantId, CancellationToken ct = default);
}

public interface ITenantAssetPurgeService
{
  Task<int> PurgeTenantAsync(Guid tenantId, string tenantConnectionString, CancellationToken ct = default);
}
