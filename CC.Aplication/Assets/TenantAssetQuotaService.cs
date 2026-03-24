using CC.Infraestructure.Tenant;
using CC.Domain.Assets;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Aplication.Assets;

public sealed class TenantAssetQuotaService : ITenantAssetQuotaService
{
  private readonly TenantDbContextFactory _dbFactory;
  private readonly ITenantPlanLimitResolver _limitResolver;

  public TenantAssetQuotaService(TenantDbContextFactory dbFactory, ITenantPlanLimitResolver limitResolver)
  {
    _dbFactory = dbFactory;
    _limitResolver = limitResolver;
  }

  public async Task EnsureUploadAllowedAsync(Guid tenantId, TenantAssetType assetType, long bytesToAdd, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();
    var snapshot = await EnsureSnapshotAsync(db, tenantId, ct);

    if (assetType == TenantAssetType.Video && !snapshot.AllowVideos)
    {
      throw new InvalidOperationException("Your current plan does not allow video uploads.");
    }

    if (assetType == TenantAssetType.Image && snapshot.MaxImageCount >= 0 && snapshot.CurrentImageCount >= snapshot.MaxImageCount)
    {
      throw new InvalidOperationException($"Image limit reached ({snapshot.MaxImageCount}).");
    }

    if (assetType == TenantAssetType.Video && snapshot.MaxVideoCount >= 0 && snapshot.CurrentVideoCount >= snapshot.MaxVideoCount)
    {
      throw new InvalidOperationException($"Video limit reached ({snapshot.MaxVideoCount}).");
    }

    if (snapshot.MaxTotalBytes >= 0 && (snapshot.CurrentTotalBytes + bytesToAdd) > snapshot.MaxTotalBytes)
    {
      throw new InvalidOperationException("Storage limit reached for your plan.");
    }
  }

  public async Task IncreaseUsageAsync(Guid tenantId, TenantAssetType assetType, long bytesToAdd, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();
    var snapshot = await EnsureSnapshotAsync(db, tenantId, ct);

    if (assetType == TenantAssetType.Image)
    {
      snapshot.CurrentImageCount += 1;
    }
    else
    {
      snapshot.CurrentVideoCount += 1;
    }

    snapshot.CurrentTotalBytes += bytesToAdd;
    snapshot.LastRecalculatedAt = DateTime.UtcNow;
    snapshot.VersionStamp += 1;

    await db.SaveChangesAsync(ct);
  }

  public async Task DecreaseUsageAsync(Guid tenantId, TenantAssetType assetType, long bytesToRemove, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();
    var snapshot = await EnsureSnapshotAsync(db, tenantId, ct);

    if (assetType == TenantAssetType.Image)
    {
      snapshot.CurrentImageCount = Math.Max(0, snapshot.CurrentImageCount - 1);
    }
    else
    {
      snapshot.CurrentVideoCount = Math.Max(0, snapshot.CurrentVideoCount - 1);
    }

    snapshot.CurrentTotalBytes = Math.Max(0, snapshot.CurrentTotalBytes - bytesToRemove);
    snapshot.LastRecalculatedAt = DateTime.UtcNow;
    snapshot.VersionStamp += 1;

    await db.SaveChangesAsync(ct);
  }

  public async Task<TenantAssetQuotaStatusDto> GetStatusAsync(Guid tenantId, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();
    var snapshot = await EnsureSnapshotAsync(db, tenantId, ct);

    return new TenantAssetQuotaStatusDto
    {
      TenantId = tenantId,
      MaxImageCount = snapshot.MaxImageCount,
      MaxVideoCount = snapshot.MaxVideoCount,
      MaxTotalBytes = snapshot.MaxTotalBytes,
      AllowVideos = snapshot.AllowVideos,
      CurrentImageCount = snapshot.CurrentImageCount,
      CurrentVideoCount = snapshot.CurrentVideoCount,
      CurrentTotalBytes = snapshot.CurrentTotalBytes,
      LastRecalculatedAt = snapshot.LastRecalculatedAt
    };
  }

  public async Task RecalculateAsync(Guid tenantId, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create();
    var snapshot = await EnsureSnapshotAsync(db, tenantId, ct);

    var activeAssets = await db.TenantAssets
        .Where(a => a.TenantId == tenantId && a.SourceType == TenantAssetSourceType.InternalStorage && a.LifecycleStatus == TenantAssetLifecycleStatus.Active)
        .ToListAsync(ct);

    snapshot.CurrentImageCount = activeAssets.Count(a => a.AssetType == TenantAssetType.Image);
    snapshot.CurrentVideoCount = activeAssets.Count(a => a.AssetType == TenantAssetType.Video);
    snapshot.CurrentTotalBytes = activeAssets.Sum(a => a.SizeBytes);
    snapshot.LastRecalculatedAt = DateTime.UtcNow;
    snapshot.VersionStamp += 1;

    await db.SaveChangesAsync(ct);
  }

  private async Task<TenantAssetQuotaSnapshot> EnsureSnapshotAsync(TenantDbContext db, Guid tenantId, CancellationToken ct)
  {
    var limits = await _limitResolver.ResolveAsync(ct);
    var snapshot = await db.TenantAssetQuotaSnapshots.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
    if (snapshot != null)
    {
      var changed = snapshot.PlanCodeSnapshot != limits.PlanCode ||
                    snapshot.MaxImageCount != limits.MaxImageCount ||
                    snapshot.MaxVideoCount != limits.MaxVideoCount ||
                    snapshot.MaxTotalBytes != limits.MaxTotalBytes ||
                    snapshot.AllowVideos != limits.AllowVideos;

      if (changed)
      {
        snapshot.PlanCodeSnapshot = limits.PlanCode;
        snapshot.MaxImageCount = limits.MaxImageCount;
        snapshot.MaxVideoCount = limits.MaxVideoCount;
        snapshot.MaxTotalBytes = limits.MaxTotalBytes;
        snapshot.AllowVideos = limits.AllowVideos;
        snapshot.LastPlanSyncAt = DateTime.UtcNow;
        snapshot.VersionStamp += 1;
        await db.SaveChangesAsync(ct);
      }

      return snapshot;
    }

    snapshot = new TenantAssetQuotaSnapshot
    {
      TenantId = tenantId,
      PlanCodeSnapshot = limits.PlanCode,
      MaxImageCount = limits.MaxImageCount,
      MaxVideoCount = limits.MaxVideoCount,
      MaxTotalBytes = limits.MaxTotalBytes,
      AllowVideos = limits.AllowVideos,
      AllowVisualModules = true,
      CurrentImageCount = 0,
      CurrentVideoCount = 0,
      CurrentTotalBytes = 0,
      LastPlanSyncAt = DateTime.UtcNow,
      LastRecalculatedAt = DateTime.UtcNow,
      VersionStamp = 1
    };

    db.TenantAssetQuotaSnapshots.Add(snapshot);
    await db.SaveChangesAsync(ct);

    return snapshot;
  }
}
