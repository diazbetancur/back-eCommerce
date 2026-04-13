using CC.Domain.Assets;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CC.Aplication.Catalog;

public interface IBannerManagementService
{
  Task<BannerResponse> CreateAsync(CreateBannerRequest request, CancellationToken ct = default);
  Task<BannerResponse> UpdateAsync(Guid id, UpdateBannerRequest request, CancellationToken ct = default);
  Task DeleteAsync(Guid id, CancellationToken ct = default);
  Task<BannerResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
  Task<BannerListResponse> GetAllAsync(
      int page,
      int pageSize,
      string? search,
      BannerPosition? position,
      bool? isActive,
      CancellationToken ct = default);
}

public class BannerManagementService : IBannerManagementService
{
  private readonly TenantDbContextFactory _dbFactory;
  private readonly ITenantAccessor _tenantAccessor;
  private readonly IAssetService _assetService;
  private readonly TenantAssetsOptions _assetsOptions;

  public BannerManagementService(
      TenantDbContextFactory dbFactory,
      ITenantAccessor tenantAccessor,
      IAssetService assetService,
      IOptions<TenantAssetsOptions> assetsOptions)
  {
    _dbFactory = dbFactory;
    _tenantAccessor = tenantAccessor;
    _assetService = assetService;
    _assetsOptions = assetsOptions.Value;
  }

  public async Task<BannerResponse> CreateAsync(CreateBannerRequest request, CancellationToken ct = default)
  {
    EnsureTenantContext();
    ValidateDateRange(request.StartDate, request.EndDate);

    if (request.ImageContent == null || request.ImageSizeBytes is null || request.ImageSizeBytes <= 0)
    {
      throw new InvalidOperationException("Banner image is required");
    }

    await using var db = _dbFactory.Create();

    var banner = new Banner
    {
      Id = Guid.NewGuid(),
      Title = request.Title.Trim(),
      Subtitle = request.Subtitle?.Trim(),
      ImageUrl = string.Empty,
      TargetUrl = request.TargetUrl?.Trim(),
      ButtonText = request.ButtonText?.Trim(),
      Position = request.Position,
      StartDate = request.StartDate,
      EndDate = request.EndDate,
      DisplayOrder = request.DisplayOrder,
      IsActive = request.IsActive,
      CreatedAt = DateTime.UtcNow
    };

    await UploadOrReplaceBannerImageAsync(banner, request, ct);

    db.Banners.Add(banner);
    await db.SaveChangesAsync(ct);

    return await GetByIdAsync(banner.Id, ct)
        ?? throw new InvalidOperationException("Could not load created banner");
  }

  public async Task<BannerResponse> UpdateAsync(Guid id, UpdateBannerRequest request, CancellationToken ct = default)
  {
    EnsureTenantContext();
    ValidateDateRange(request.StartDate, request.EndDate);

    await using var db = _dbFactory.Create();

    var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
    if (banner == null)
    {
      throw new InvalidOperationException("Banner not found");
    }

    banner.Title = request.Title.Trim();
    banner.Subtitle = request.Subtitle?.Trim();
    banner.TargetUrl = request.TargetUrl?.Trim();
    banner.ButtonText = request.ButtonText?.Trim();
    banner.Position = request.Position;
    banner.StartDate = request.StartDate;
    banner.EndDate = request.EndDate;
    banner.DisplayOrder = request.DisplayOrder;
    banner.IsActive = request.IsActive;

    await UploadOrReplaceBannerImageAsync(banner, request, ct);
    await db.SaveChangesAsync(ct);

    return await GetByIdAsync(banner.Id, ct)
        ?? throw new InvalidOperationException("Could not load updated banner");
  }

  public async Task DeleteAsync(Guid id, CancellationToken ct = default)
  {
    EnsureTenantContext();

    await using var db = _dbFactory.Create();
    var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
    if (banner == null)
    {
      throw new InvalidOperationException("Banner not found");
    }

    await EnsureBannerAssetsDeletedOrThrowAsync(db, id, ct);

    db.Banners.Remove(banner);
    await db.SaveChangesAsync(ct);
  }

  public async Task<BannerResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    EnsureTenantContext();

    await using var db = _dbFactory.Create();

    var banner = await db.Banners
        .AsNoTracking()
        .FirstOrDefaultAsync(b => b.Id == id, ct);

    if (banner == null)
    {
      return null;
    }

    var imageUrl = await ResolveBannerImagePublicUrlAsync(db, banner.Id, banner.ImageUrl, ct);

    return new BannerResponse
    {
      Id = banner.Id,
      Title = banner.Title,
      Subtitle = banner.Subtitle,
      ImageUrl = imageUrl ?? string.Empty,
      TargetUrl = banner.TargetUrl,
      ButtonText = banner.ButtonText,
      Position = banner.Position.ToString().ToLowerInvariant(),
      StartDate = banner.StartDate,
      EndDate = banner.EndDate,
      DisplayOrder = banner.DisplayOrder,
      IsActive = banner.IsActive,
      CreatedAt = banner.CreatedAt
    };
  }

  public async Task<BannerListResponse> GetAllAsync(
      int page,
      int pageSize,
      string? search,
      BannerPosition? position,
      bool? isActive,
      CancellationToken ct = default)
  {
    EnsureTenantContext();

    await using var db = _dbFactory.Create();

    var query = db.Banners.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
      var searchText = search.Trim().ToLowerInvariant();
      query = query.Where(b => b.Title.ToLower().Contains(searchText) ||
                               (b.Subtitle != null && b.Subtitle.ToLower().Contains(searchText)));
    }

    if (position.HasValue)
    {
      query = query.Where(b => b.Position == position.Value);
    }

    if (isActive.HasValue)
    {
      query = query.Where(b => b.IsActive == isActive.Value);
    }

    var total = await query.CountAsync(ct);

    var banners = await query
        .OrderBy(b => b.DisplayOrder)
        .ThenByDescending(b => b.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);

    var imageMap = await BuildBannerImageUrlMapAsync(db, banners.Select(b => b.Id).ToList(), ct);

    return new BannerListResponse
    {
      Items = banners.Select(b => new BannerListItem
      {
        Id = b.Id,
        Title = b.Title,
        ImageUrl = imageMap.TryGetValue(b.Id, out var imageUrl)
            ? imageUrl
            : (BuildPublicUrlFromStorageKey(b.ImageUrl) ?? b.ImageUrl),
        Position = b.Position.ToString().ToLowerInvariant(),
        DisplayOrder = b.DisplayOrder,
        IsActive = b.IsActive,
        StartDate = b.StartDate,
        EndDate = b.EndDate
      }).ToList(),
      Total = total,
      Page = page,
      PageSize = pageSize
    };
  }

  private async Task UploadOrReplaceBannerImageAsync(
      Banner banner,
      CreateBannerRequest request,
      CancellationToken ct)
  {
    if (request.ImageContent == null || request.ImageSizeBytes is null || request.ImageSizeBytes <= 0)
    {
      return;
    }

    EnsureTenantContext();

    if (request.ImageContent.CanSeek)
    {
      request.ImageContent.Position = 0;
    }

    var existingAssets = await _assetService.ListByEntityAsync(
        _tenantAccessor.TenantInfo!.Id,
        module: "banner",
        entityType: "banner",
        entityId: banner.Id.ToString(),
        ct);

    var uploaded = await _assetService.UploadAsync(new UploadAssetCommand
    {
      TenantId = _tenantAccessor.TenantInfo.Id,
      UploadedByUserId = string.IsNullOrWhiteSpace(request.UploadedByUserId) ? "system" : request.UploadedByUserId!,
      Module = "banner",
      EntityType = "banner",
      EntityId = banner.Id.ToString(),
      AssetType = TenantAssetType.Image,
      Visibility = TenantAssetVisibility.Public,
      OriginalFileName = request.ImageFileName ?? "banner-image",
      ContentType = request.ImageContentType ?? "application/octet-stream",
      SizeBytes = request.ImageSizeBytes.Value,
      Content = request.ImageContent!,
      SetAsPrimary = true
    }, ct);

    foreach (var existingAsset in existingAssets)
    {
      await _assetService.DeleteSingleAsync(_tenantAccessor.TenantInfo.Id, existingAsset.Id, ct);
    }

    banner.ImageUrl = uploaded.StorageKey ?? string.Empty;
  }

  private Task UploadOrReplaceBannerImageAsync(
      Banner banner,
      UpdateBannerRequest request,
      CancellationToken ct)
  {
    return UploadOrReplaceBannerImageAsync(
        banner,
        new CreateBannerRequest
        {
          UploadedByUserId = request.UploadedByUserId,
          ImageFileName = request.ImageFileName,
          ImageContentType = request.ImageContentType,
          ImageSizeBytes = request.ImageSizeBytes,
          ImageContent = request.ImageContent
        },
        ct);
  }

  private async Task<string?> ResolveBannerImagePublicUrlAsync(
      TenantDbContext db,
      Guid bannerId,
      string? storedImageValue,
      CancellationToken ct)
  {
    var entityId = bannerId.ToString().ToLowerInvariant();
    var asset = await db.TenantAssets
        .AsNoTracking()
        .Where(a => a.Module == "banner" &&
                    a.EntityType == "banner" &&
                    a.EntityId == entityId &&
                    a.LifecycleStatus == TenantAssetLifecycleStatus.Active)
        .OrderByDescending(a => a.CreatedAt)
        .FirstOrDefaultAsync(ct);

    if (asset == null)
    {
      return BuildPublicUrlFromStorageKey(storedImageValue) ?? storedImageValue;
    }

    return BuildPublicUrlFromStorageKey(asset.StorageKey)
        ?? asset.PublicUrl
        ?? asset.UrlOrPath;
  }

  private async Task<Dictionary<Guid, string>> BuildBannerImageUrlMapAsync(
      TenantDbContext db,
      IReadOnlyCollection<Guid> bannerIds,
      CancellationToken ct)
  {
    if (bannerIds.Count == 0)
    {
      return new Dictionary<Guid, string>();
    }

    var entityIds = bannerIds.Select(x => x.ToString().ToLowerInvariant()).ToHashSet();
    var assets = await db.TenantAssets
        .AsNoTracking()
        .Where(a => a.Module == "banner" &&
                    a.EntityType == "banner" &&
                    a.EntityId != null &&
                    entityIds.Contains(a.EntityId) &&
                    a.LifecycleStatus == TenantAssetLifecycleStatus.Active)
        .OrderByDescending(a => a.CreatedAt)
        .ToListAsync(ct);

    var map = new Dictionary<Guid, string>();
    foreach (var asset in assets)
    {
      if (asset.EntityId == null || !Guid.TryParse(asset.EntityId, out var bannerId) || map.ContainsKey(bannerId))
      {
        continue;
      }

      var publicUrl = BuildPublicUrlFromStorageKey(asset.StorageKey)
          ?? asset.PublicUrl
          ?? asset.UrlOrPath;

      if (!string.IsNullOrWhiteSpace(publicUrl))
      {
        map[bannerId] = publicUrl;
      }
    }

    return map;
  }

  private string? BuildPublicUrlFromStorageKey(string? storageKey)
  {
    if (string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(_assetsOptions.CloudflareR2.PublicBaseUrl))
    {
      return null;
    }

    return $"{_assetsOptions.CloudflareR2.PublicBaseUrl.TrimEnd('/')}/{storageKey.TrimStart('/')}";
  }

  private async Task EnsureBannerAssetsDeletedOrThrowAsync(
      TenantDbContext db,
      Guid bannerId,
      CancellationToken ct)
  {
    if (_tenantAccessor.TenantInfo == null)
    {
      throw new InvalidOperationException("Tenant context not available");
    }

    var normalizedEntityId = bannerId.ToString().ToLowerInvariant();
    var pendingAssets = await db.TenantAssets
        .AsNoTracking()
        .Where(a => a.Module == "banner" &&
                    a.EntityType == "banner" &&
                    a.EntityId == normalizedEntityId &&
                    a.LifecycleStatus != TenantAssetLifecycleStatus.PhysicallyDeleted)
        .CountAsync(ct);

    if (pendingAssets == 0)
    {
      return;
    }

    var deletedCount = await _assetService.PurgeByEntityAsync(
        _tenantAccessor.TenantInfo.Id,
        module: "banner",
        entityType: "banner",
        entityId: bannerId.ToString(),
        ct);

    var remainingAssets = await db.TenantAssets
        .AsNoTracking()
        .Where(a => a.Module == "banner" &&
                    a.EntityType == "banner" &&
                    a.EntityId == normalizedEntityId &&
                    a.LifecycleStatus != TenantAssetLifecycleStatus.PhysicallyDeleted)
        .CountAsync(ct);

    if (deletedCount < pendingAssets || remainingAssets > 0)
    {
      throw new InvalidOperationException("Unable to delete all related banner assets. Banner deletion was canceled.");
    }
  }

  private void EnsureTenantContext()
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      throw new InvalidOperationException("Tenant context not available");
    }
  }

  private static void ValidateDateRange(DateTime? startDate, DateTime? endDate)
  {
    if (startDate.HasValue && endDate.HasValue && startDate > endDate)
    {
      throw new InvalidOperationException("StartDate must be less than or equal to EndDate");
    }
  }
}

public class CreateBannerRequest
{
  public string Title { get; init; } = string.Empty;
  public string? Subtitle { get; init; }
  public string? TargetUrl { get; init; }
  public string? ButtonText { get; init; }
  public BannerPosition Position { get; init; } = BannerPosition.Hero;
  public DateTime? StartDate { get; init; }
  public DateTime? EndDate { get; init; }
  public int DisplayOrder { get; init; }
  public bool IsActive { get; init; } = true;
  public string? UploadedByUserId { get; init; }
  public string? ImageFileName { get; init; }
  public string? ImageContentType { get; init; }
  public long? ImageSizeBytes { get; init; }
  public Stream? ImageContent { get; init; }
}

public class UpdateBannerRequest
{
  public string Title { get; init; } = string.Empty;
  public string? Subtitle { get; init; }
  public string? TargetUrl { get; init; }
  public string? ButtonText { get; init; }
  public BannerPosition Position { get; init; } = BannerPosition.Hero;
  public DateTime? StartDate { get; init; }
  public DateTime? EndDate { get; init; }
  public int DisplayOrder { get; init; }
  public bool IsActive { get; init; } = true;
  public string? UploadedByUserId { get; init; }
  public string? ImageFileName { get; init; }
  public string? ImageContentType { get; init; }
  public long? ImageSizeBytes { get; init; }
  public Stream? ImageContent { get; init; }
}

public class BannerResponse
{
  public Guid Id { get; set; }
  public string Title { get; set; } = string.Empty;
  public string? Subtitle { get; set; }
  public string ImageUrl { get; set; } = string.Empty;
  public string? TargetUrl { get; set; }
  public string? ButtonText { get; set; }
  public string Position { get; set; } = "hero";
  public DateTime? StartDate { get; set; }
  public DateTime? EndDate { get; set; }
  public int DisplayOrder { get; set; }
  public bool IsActive { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class BannerListItem
{
  public Guid Id { get; set; }
  public string Title { get; set; } = string.Empty;
  public string? ImageUrl { get; set; }
  public string Position { get; set; } = "hero";
  public int DisplayOrder { get; set; }
  public bool IsActive { get; set; }
  public DateTime? StartDate { get; set; }
  public DateTime? EndDate { get; set; }
}

public class BannerListResponse
{
  public List<BannerListItem> Items { get; set; } = new();
  public int Total { get; set; }
  public int Page { get; set; }
  public int PageSize { get; set; }
}
