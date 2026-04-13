using CC.Domain.Assets;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CC.Aplication.Catalog;

public interface IPopupManagementService
{
  Task<PopupResponse> CreateAsync(CreatePopupRequest request, CancellationToken ct = default);
  Task<PopupResponse> UpdateAsync(Guid id, UpdatePopupRequest request, CancellationToken ct = default);
  Task DeleteAsync(Guid id, CancellationToken ct = default);
  Task<PopupResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
  Task<PopupListResponse> GetAllAsync(int page, int pageSize, bool? isActive, CancellationToken ct = default);
  Task<PopupPublicResponse?> GetActiveForStorefrontAsync(CancellationToken ct = default);
}

public class PopupManagementService : IPopupManagementService
{
  private const string PopupModule = "popup";
  private const string PopupEntityType = "popup";

  private readonly TenantDbContextFactory _dbFactory;
  private readonly ITenantAccessor _tenantAccessor;
  private readonly IAssetService _assetService;
  private readonly TenantAssetsOptions _assetsOptions;

  public PopupManagementService(
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

  public async Task<PopupResponse> CreateAsync(CreatePopupRequest request, CancellationToken ct = default)
  {
    EnsureTenantContext();
    ValidateDateRange(request.StartDate, request.EndDate);

    if (request.ImageContent == null || request.ImageSizeBytes is null || request.ImageSizeBytes <= 0)
    {
      throw new InvalidOperationException("Popup image is required");
    }

    await using var db = _dbFactory.Create();

    if (request.IsActive)
    {
      await DeactivateOtherPopupsAsync(db, exceptId: null, ct);
    }

    var popup = new Popup
    {
      Id = Guid.NewGuid(),
      ImageUrl = string.Empty,
      TargetUrl = request.TargetUrl?.Trim(),
      ButtonText = request.ButtonText?.Trim(),
      StartDate = request.StartDate,
      EndDate = request.EndDate,
      IsActive = request.IsActive,
      CreatedAt = DateTime.UtcNow
    };

    await UploadOrReplacePopupImageAsync(popup, request, ct);

    db.Popups.Add(popup);
    await db.SaveChangesAsync(ct);

    return await GetByIdAsync(popup.Id, ct)
      ?? throw new InvalidOperationException("Could not load created popup");
  }

  public async Task<PopupResponse> UpdateAsync(Guid id, UpdatePopupRequest request, CancellationToken ct = default)
  {
    EnsureTenantContext();
    ValidateDateRange(request.StartDate, request.EndDate);

    if (request.ImageContent == null || request.ImageSizeBytes is null || request.ImageSizeBytes <= 0)
    {
      throw new InvalidOperationException("Popup image is required");
    }

    await using var db = _dbFactory.Create();

    var popup = await db.Popups.FirstOrDefaultAsync(p => p.Id == id, ct);
    if (popup == null)
    {
      throw new InvalidOperationException("Popup not found");
    }

    if (request.IsActive)
    {
      await DeactivateOtherPopupsAsync(db, exceptId: id, ct);
    }

    popup.TargetUrl = request.TargetUrl?.Trim();
    popup.ButtonText = request.ButtonText?.Trim();
    popup.StartDate = request.StartDate;
    popup.EndDate = request.EndDate;
    popup.IsActive = request.IsActive;

    await UploadOrReplacePopupImageAsync(popup, request, ct);
    await db.SaveChangesAsync(ct);

    return await GetByIdAsync(popup.Id, ct)
      ?? throw new InvalidOperationException("Could not load updated popup");
  }

  public async Task DeleteAsync(Guid id, CancellationToken ct = default)
  {
    EnsureTenantContext();

    if (_tenantAccessor.TenantInfo != null)
    {
      await _assetService.PurgeByEntityAsync(
          _tenantAccessor.TenantInfo.Id,
          module: PopupModule,
          entityType: PopupEntityType,
          entityId: id.ToString(),
          ct);
    }

    await using var db = _dbFactory.Create();
    var popup = await db.Popups.FirstOrDefaultAsync(p => p.Id == id, ct);
    if (popup == null)
    {
      throw new InvalidOperationException("Popup not found");
    }

    db.Popups.Remove(popup);
    await db.SaveChangesAsync(ct);
  }

  public async Task<PopupResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    EnsureTenantContext();

    await using var db = _dbFactory.Create();

    var popup = await db.Popups
      .AsNoTracking()
      .FirstOrDefaultAsync(p => p.Id == id, ct);

    if (popup == null)
    {
      return null;
    }

    var imageUrl = await ResolvePopupImagePublicUrlAsync(db, popup.Id, popup.ImageUrl, ct);

    return new PopupResponse
    {
      Id = popup.Id,
      ImageUrl = imageUrl,
      TargetUrl = popup.TargetUrl,
      ButtonText = popup.ButtonText,
      StartDate = popup.StartDate,
      EndDate = popup.EndDate,
      IsActive = popup.IsActive,
      CreatedAt = popup.CreatedAt
    };
  }

  public async Task<PopupListResponse> GetAllAsync(int page, int pageSize, bool? isActive, CancellationToken ct = default)
  {
    EnsureTenantContext();

    await using var db = _dbFactory.Create();

    var query = db.Popups.AsNoTracking().AsQueryable();

    if (isActive.HasValue)
    {
      query = query.Where(p => p.IsActive == isActive.Value);
    }

    var total = await query.CountAsync(ct);

    var items = await query
      .OrderByDescending(p => p.CreatedAt)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync(ct);

    var imageMap = await BuildPopupImageUrlMapAsync(db, items.Select(i => i.Id).ToList(), ct);

    return new PopupListResponse
    {
      Items = items.Select(p => new PopupListItem
      {
        Id = p.Id,
        ImageUrl = imageMap.TryGetValue(p.Id, out var imageUrl)
          ? imageUrl
          : (BuildPublicUrlFromStorageKey(p.ImageUrl) ?? p.ImageUrl),
        TargetUrl = p.TargetUrl,
        ButtonText = p.ButtonText,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt
      }).ToList(),
      Total = total,
      Page = page,
      PageSize = pageSize
    };
  }

  public async Task<PopupPublicResponse?> GetActiveForStorefrontAsync(CancellationToken ct = default)
  {
    EnsureTenantContext();

    await using var db = _dbFactory.Create();
    var now = DateTime.UtcNow;

    var expiredActives = await db.Popups
      .Where(p => p.IsActive && p.EndDate.HasValue && p.EndDate.Value < now)
      .ToListAsync(ct);

    if (expiredActives.Count > 0)
    {
      foreach (var expired in expiredActives)
      {
        expired.IsActive = false;
      }

      await db.SaveChangesAsync(ct);
    }

    var popup = await db.Popups
      .AsNoTracking()
      .Where(p => p.IsActive)
      .Where(p => p.StartDate == null || p.StartDate <= now)
      .Where(p => p.EndDate == null || p.EndDate >= now)
      .OrderByDescending(p => p.CreatedAt)
      .FirstOrDefaultAsync(ct);

    if (popup == null)
    {
      return null;
    }

    var imageUrl = await ResolvePopupImagePublicUrlAsync(db, popup.Id, popup.ImageUrl, ct);

    return new PopupPublicResponse
    {
      Id = popup.Id,
      ImageUrl = imageUrl,
      TargetUrl = popup.TargetUrl,
      ButtonText = popup.ButtonText
    };
  }

  private static async Task DeactivateOtherPopupsAsync(TenantDbContext db, Guid? exceptId, CancellationToken ct)
  {
    var others = await db.Popups
      .Where(p => p.IsActive && (!exceptId.HasValue || p.Id != exceptId.Value))
      .ToListAsync(ct);

    if (others.Count == 0)
    {
      return;
    }

    foreach (var popup in others)
    {
      popup.IsActive = false;
    }
  }

  private async Task UploadOrReplacePopupImageAsync(Popup popup, CreatePopupRequest request, CancellationToken ct)
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
      module: PopupModule,
      entityType: PopupEntityType,
      entityId: popup.Id.ToString(),
      ct);

    var uploaded = await _assetService.UploadAsync(new UploadAssetCommand
    {
      TenantId = _tenantAccessor.TenantInfo.Id,
      UploadedByUserId = string.IsNullOrWhiteSpace(request.UploadedByUserId) ? "system" : request.UploadedByUserId!,
      Module = PopupModule,
      EntityType = PopupEntityType,
      EntityId = popup.Id.ToString(),
      AssetType = TenantAssetType.Image,
      Visibility = TenantAssetVisibility.Public,
      OriginalFileName = request.ImageFileName ?? "popup-image",
      ContentType = request.ImageContentType ?? "application/octet-stream",
      SizeBytes = request.ImageSizeBytes.Value,
      Content = request.ImageContent,
      SetAsPrimary = true
    }, ct);

    foreach (var existingAsset in existingAssets)
    {
      await _assetService.DeleteSingleAsync(_tenantAccessor.TenantInfo.Id, existingAsset.Id, ct);
    }

    popup.ImageUrl = uploaded.StorageKey;
  }

  private Task UploadOrReplacePopupImageAsync(Popup popup, UpdatePopupRequest request, CancellationToken ct)
  {
    return UploadOrReplacePopupImageAsync(
      popup,
      new CreatePopupRequest
      {
        UploadedByUserId = request.UploadedByUserId,
        ImageFileName = request.ImageFileName,
        ImageContentType = request.ImageContentType,
        ImageSizeBytes = request.ImageSizeBytes,
        ImageContent = request.ImageContent
      },
      ct);
  }

  private async Task<string?> ResolvePopupImagePublicUrlAsync(
      TenantDbContext db,
      Guid popupId,
      string? storedImageValue,
      CancellationToken ct)
  {
    var entityId = popupId.ToString().ToLowerInvariant();
    var asset = await db.TenantAssets
      .AsNoTracking()
      .Where(a => a.Module == PopupModule &&
          a.EntityType == PopupEntityType &&
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

  private async Task<Dictionary<Guid, string>> BuildPopupImageUrlMapAsync(
      TenantDbContext db,
      IReadOnlyCollection<Guid> popupIds,
      CancellationToken ct)
  {
    if (popupIds.Count == 0)
    {
      return new Dictionary<Guid, string>();
    }

    var entityIds = popupIds.Select(x => x.ToString().ToLowerInvariant()).ToHashSet();
    var assets = await db.TenantAssets
      .AsNoTracking()
      .Where(a => a.Module == PopupModule &&
          a.EntityType == PopupEntityType &&
                  a.EntityId != null &&
                  entityIds.Contains(a.EntityId) &&
                  a.LifecycleStatus == TenantAssetLifecycleStatus.Active)
      .OrderByDescending(a => a.CreatedAt)
      .ToListAsync(ct);

    var map = new Dictionary<Guid, string>();
    foreach (var asset in assets)
    {
      if (asset.EntityId == null || !Guid.TryParse(asset.EntityId, out var popupId) || map.ContainsKey(popupId))
      {
        continue;
      }

      var publicUrl = BuildPublicUrlFromStorageKey(asset.StorageKey)
        ?? asset.PublicUrl
        ?? asset.UrlOrPath;

      if (!string.IsNullOrWhiteSpace(publicUrl))
      {
        map[popupId] = publicUrl;
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

public class CreatePopupRequest
{
  public string? TargetUrl { get; init; }
  public string? ButtonText { get; init; }
  public DateTime? StartDate { get; init; }
  public DateTime? EndDate { get; init; }
  public bool IsActive { get; init; } = false;
  public string? UploadedByUserId { get; init; }
  public string? ImageFileName { get; init; }
  public string? ImageContentType { get; init; }
  public long? ImageSizeBytes { get; init; }
  public Stream? ImageContent { get; init; }
}

public class UpdatePopupRequest
{
  public string? TargetUrl { get; init; }
  public string? ButtonText { get; init; }
  public DateTime? StartDate { get; init; }
  public DateTime? EndDate { get; init; }
  public bool IsActive { get; init; } = false;
  public string? UploadedByUserId { get; init; }
  public string? ImageFileName { get; init; }
  public string? ImageContentType { get; init; }
  public long? ImageSizeBytes { get; init; }
  public Stream? ImageContent { get; init; }
}

public class PopupResponse
{
  public Guid Id { get; set; }
  public string? ImageUrl { get; set; }
  public string? TargetUrl { get; set; }
  public string? ButtonText { get; set; }
  public DateTime? StartDate { get; set; }
  public DateTime? EndDate { get; set; }
  public bool IsActive { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class PopupListItem
{
  public Guid Id { get; set; }
  public string? ImageUrl { get; set; }
  public string? TargetUrl { get; set; }
  public string? ButtonText { get; set; }
  public DateTime? StartDate { get; set; }
  public DateTime? EndDate { get; set; }
  public bool IsActive { get; set; }
  public DateTime CreatedAt { get; set; }
}

public class PopupListResponse
{
  public List<PopupListItem> Items { get; set; } = new();
  public int Total { get; set; }
  public int Page { get; set; }
  public int PageSize { get; set; }
}

public class PopupPublicResponse
{
  public Guid Id { get; set; }
  public string? ImageUrl { get; set; }
  public string? TargetUrl { get; set; }
  public string? ButtonText { get; set; }
}
