using CC.Infraestructure.Tenant;
using CC.Domain.Assets;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Assets;

public sealed class TenantAssetPurgeService : ITenantAssetPurgeService
{
  private readonly TenantDbContextFactory _dbFactory;
  private readonly IFileStorageProvider _storageProvider;
  private readonly ILogger<TenantAssetPurgeService> _logger;

  public TenantAssetPurgeService(
      TenantDbContextFactory dbFactory,
      IFileStorageProvider storageProvider,
      ILogger<TenantAssetPurgeService> logger)
  {
    _dbFactory = dbFactory;
    _storageProvider = storageProvider;
    _logger = logger;
  }

  public async Task<int> PurgeTenantAsync(Guid tenantId, string tenantConnectionString, CancellationToken ct = default)
  {
    await using var db = _dbFactory.Create(tenantConnectionString);

    var assets = await db.TenantAssets
        .Where(a => a.TenantId == tenantId &&
                    a.SourceType == TenantAssetSourceType.InternalStorage &&
                    a.LifecycleStatus != TenantAssetLifecycleStatus.PhysicallyDeleted)
        .ToListAsync(ct);

    var deleted = 0;
    foreach (var asset in assets)
    {
      if (string.IsNullOrWhiteSpace(asset.StorageKey))
      {
        asset.LifecycleStatus = TenantAssetLifecycleStatus.PhysicallyDeleted;
        asset.PhysicalDeletionExecuted = true;
        asset.PhysicalDeletionExecutedAt = DateTime.UtcNow;
        asset.DeletedAt = DateTime.UtcNow;
        deleted++;
        continue;
      }

      try
      {
        await _storageProvider.DeleteAsync(asset.StorageKey, ct);
        asset.LifecycleStatus = TenantAssetLifecycleStatus.PhysicallyDeleted;
        asset.PhysicalDeletionExecuted = true;
        asset.PhysicalDeletionExecutedAt = DateTime.UtcNow;
        asset.DeletedAt = DateTime.UtcNow;
        asset.PhysicalDeletionLastError = null;
        deleted++;
      }
      catch (Exception ex)
      {
        asset.LifecycleStatus = TenantAssetLifecycleStatus.DeletionFailed;
        asset.PhysicalDeletionAttempts += 1;
        asset.PhysicalDeletionLastError = ex.Message;
        _logger.LogError(ex, "Failed to purge tenant asset {AssetId} (tenant {TenantId})", asset.Id, tenantId);
      }
    }

    await db.SaveChangesAsync(ct);
    return deleted;
  }
}
