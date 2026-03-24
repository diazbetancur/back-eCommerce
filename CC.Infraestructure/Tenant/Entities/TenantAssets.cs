using CC.Domain.Assets;

namespace CC.Infraestructure.Tenant.Entities;

public class TenantAsset
{
  public Guid Id { get; set; }
  public Guid TenantId { get; set; }
  public TenantAssetType AssetType { get; set; }
  public TenantAssetSourceType SourceType { get; set; }
  public string Module { get; set; } = string.Empty;
  public string? EntityType { get; set; }
  public string? EntityId { get; set; }
  public string OriginalFileName { get; set; } = string.Empty;
  public string SafeFileName { get; set; } = string.Empty;
  public string? StorageKey { get; set; }
  public string? StorageBucket { get; set; }
  public string? PublicUrl { get; set; }
  public string UrlOrPath { get; set; } = string.Empty;
  public long SizeBytes { get; set; }
  public string Extension { get; set; } = string.Empty;
  public string ContentType { get; set; } = string.Empty;
  public string Provider { get; set; } = string.Empty;
  public TenantAssetVisibility Visibility { get; set; } = TenantAssetVisibility.Public;
  public TenantAssetLifecycleStatus LifecycleStatus { get; set; } = TenantAssetLifecycleStatus.Active;
  public bool PhysicalDeletionRequired { get; set; } = true;
  public bool PhysicalDeletionExecuted { get; set; }
  public DateTime? PhysicalDeletionExecutedAt { get; set; }
  public int PhysicalDeletionAttempts { get; set; }
  public string? PhysicalDeletionLastError { get; set; }
  public string UploadedByUserId { get; set; } = string.Empty;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? DeletedAt { get; set; }
}

public class TenantAssetQuotaSnapshot
{
  public Guid TenantId { get; set; }
  public string PlanCodeSnapshot { get; set; } = string.Empty;
  public int MaxImageCount { get; set; }
  public int MaxVideoCount { get; set; }
  public long MaxTotalBytes { get; set; }
  public bool AllowVideos { get; set; }
  public bool AllowVisualModules { get; set; }
  public int CurrentImageCount { get; set; }
  public int CurrentVideoCount { get; set; }
  public long CurrentTotalBytes { get; set; }
  public DateTime LastPlanSyncAt { get; set; } = DateTime.UtcNow;
  public DateTime LastRecalculatedAt { get; set; } = DateTime.UtcNow;
  public long VersionStamp { get; set; }
}
