using CC.Aplication.Plans;
using CC.Domain.Assets;
using CC.Infraestructure.Admin.Entities;

namespace CC.Aplication.Assets;

public sealed class TenantPlanLimitResolver : ITenantPlanLimitResolver
{
  private readonly IPlanLimitService _planLimitService;

  public TenantPlanLimitResolver(IPlanLimitService planLimitService)
  {
    _planLimitService = planLimitService;
  }

  public async Task<PlanAssetLimits> ResolveAsync(CancellationToken ct = default)
  {
    var all = await _planLimitService.GetAllLimitsAsync();

    var maxImagesRaw = all.TryGetValue(PlanLimitCodes.MaxProductImages, out var img) ? img : -1;
    var maxVideosRaw = all.TryGetValue(PlanLimitCodes.MaxProductVideos, out var vid) ? vid : 0;

    long maxTotalBytes;
    if (all.TryGetValue(PlanLimitCodes.MaxStorageBytes, out var maxStorageBytes))
    {
      maxTotalBytes = maxStorageBytes;
    }
    else
    {
      var maxStorageMb = all.TryGetValue(PlanLimitCodes.MaxStorageMB, out var stg) ? stg : -1;
      maxTotalBytes = maxStorageMb < 0 ? -1 : maxStorageMb * 1024L * 1024L;
    }

    return new PlanAssetLimits
    {
      PlanCode = "snapshot",
      MaxImageCount = maxImagesRaw < 0 ? -1 : (int)Math.Min(maxImagesRaw, int.MaxValue),
      MaxVideoCount = maxVideosRaw < 0 ? -1 : (int)Math.Min(maxVideosRaw, int.MaxValue),
      MaxTotalBytes = maxTotalBytes,
      AllowVideos = maxVideosRaw != 0
    };
  }
}
