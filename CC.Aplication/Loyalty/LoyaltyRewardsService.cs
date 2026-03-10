using CC.Domain.Entities;
using CC.Domain.Enums;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Loyalty
{
  public interface ILoyaltyRewardsService
  {
    // CRUD Rewards
    Task<LoyaltyRewardDto> CreateRewardAsync(CreateLoyaltyRewardRequest request, CancellationToken ct = default);
    Task<LoyaltyRewardDto> UpdateRewardAsync(Guid id, UpdateLoyaltyRewardRequest request, CancellationToken ct = default);
    Task DeleteRewardAsync(Guid id, CancellationToken ct = default);
    Task<LoyaltyRewardDto?> GetRewardByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedLoyaltyRewardsResponse> GetRewardsAsync(GetLoyaltyRewardsQuery query, CancellationToken ct = default);

    // Redemption
    Task<RedeemRewardResponse> RedeemRewardAsync(Guid userId, Guid rewardId, CancellationToken ct = default);
    Task<LoyaltyRedemptionDto?> GetRedemptionByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedLoyaltyRedemptionsResponse> GetRedemptionsAsync(GetLoyaltyRedemptionsQuery query, CancellationToken ct = default);
    Task<LoyaltyRedemptionDto> UpdateRedemptionStatusAsync(Guid id, UpdateRedemptionStatusRequest request, CancellationToken ct = default);
  }

  public class LoyaltyRewardsService : ILoyaltyRewardsService
  {
    private readonly TenantDbContextFactory _dbFactory;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<LoyaltyRewardsService> _logger;

    public LoyaltyRewardsService(
        TenantDbContextFactory dbFactory,
        ITenantAccessor tenantAccessor,
        ILogger<LoyaltyRewardsService> logger)
    {
      _dbFactory = dbFactory;
      _tenantAccessor = tenantAccessor;
      _logger = logger;
    }

    // ==================== REWARDS CRUD ====================

    public async Task<LoyaltyRewardDto> CreateRewardAsync(CreateLoyaltyRewardRequest request, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      // Validar tipo de premio
      if (!LoyaltyRewardType.IsValid(request.RewardType))
        throw new ArgumentException($"Invalid reward type: {request.RewardType}");

      ValidateAvailabilityWindow(request.AvailableFrom, request.AvailableUntil);

      var stockToPersist = ResolveStock(request.Stock, request.CouponQuantity);
      var normalizedProductIds = NormalizeProductIds(request.ProductIds);
      var resolvedProductId = ResolveProductIdForProductReward(
        request.RewardType,
        normalizedProductIds);
      ValidateRewardTargeting(
        request.RewardType,
        resolvedProductId,
        normalizedProductIds,
        request.DiscountValue,
        request.AppliesToAllEligibleProducts,
        request.SingleProductSelectionRule);

      await using var db = _dbFactory.Create();

      var reward = new LoyaltyReward
      {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Description = request.Description,
        PointsCost = request.PointsCost,
        RewardType = request.RewardType.ToUpper(),
        ProductId = resolvedProductId,
        AppliesToAllEligibleProducts = request.AppliesToAllEligibleProducts,
        SingleProductSelectionRule = request.SingleProductSelectionRule?.ToUpper(),
        DiscountValue = request.DiscountValue,
        ImageUrl = request.ImageUrl,
        IsActive = request.IsActive,
        Stock = stockToPersist,
        ValidityDays = request.ValidityDays,
        AvailableFrom = request.AvailableFrom,
        AvailableUntil = request.AvailableUntil,
        DisplayOrder = request.DisplayOrder,
        DateCreated = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };

      db.LoyaltyRewards.Add(reward);

      if (normalizedProductIds.Count > 0)
      {
        var rewardProducts = normalizedProductIds
          .Select(productId => new LoyaltyRewardProduct
          {
            RewardId = reward.Id,
            ProductId = productId,
            DateCreated = DateTime.UtcNow
          });

        db.LoyaltyRewardProducts.AddRange(rewardProducts);
      }

      await db.SaveChangesAsync(ct);

      _logger.LogInformation("Created loyalty reward {RewardId}: {Name}", reward.Id, reward.Name);

      return await MapToDto(db, reward, ct);
    }

    public async Task<LoyaltyRewardDto> UpdateRewardAsync(Guid id, UpdateLoyaltyRewardRequest request, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      if (!LoyaltyRewardType.IsValid(request.RewardType))
        throw new ArgumentException($"Invalid reward type: {request.RewardType}");

      ValidateAvailabilityWindow(request.AvailableFrom, request.AvailableUntil);

      var stockToPersist = ResolveStock(request.Stock, request.CouponQuantity);
      var normalizedProductIds = NormalizeProductIds(request.ProductIds);
      var resolvedProductId = ResolveProductIdForProductReward(
        request.RewardType,
        normalizedProductIds);
      ValidateRewardTargeting(
        request.RewardType,
        resolvedProductId,
        normalizedProductIds,
        request.DiscountValue,
        request.AppliesToAllEligibleProducts,
        request.SingleProductSelectionRule);

      await using var db = _dbFactory.Create();

      var reward = await db.LoyaltyRewards.FindAsync(new object[] { id }, ct);
      if (reward == null)
        throw new KeyNotFoundException($"Reward {id} not found");

      reward.Name = request.Name;
      reward.Description = request.Description;
      reward.PointsCost = request.PointsCost;
      reward.RewardType = request.RewardType.ToUpper();
      reward.ProductId = resolvedProductId;
      reward.AppliesToAllEligibleProducts = request.AppliesToAllEligibleProducts;
      reward.SingleProductSelectionRule = request.SingleProductSelectionRule?.ToUpper();
      reward.DiscountValue = request.DiscountValue;
      reward.ImageUrl = request.ImageUrl;
      reward.IsActive = request.IsActive;
      reward.Stock = stockToPersist;
      reward.ValidityDays = request.ValidityDays;
      reward.AvailableFrom = request.AvailableFrom;
      reward.AvailableUntil = request.AvailableUntil;
      reward.DisplayOrder = request.DisplayOrder;
      reward.UpdatedAt = DateTime.UtcNow;

      var existingRewardProducts = await db.LoyaltyRewardProducts
        .Where(p => p.RewardId == reward.Id)
        .ToListAsync(ct);

      if (existingRewardProducts.Count > 0)
      {
        db.LoyaltyRewardProducts.RemoveRange(existingRewardProducts);
      }

      if (normalizedProductIds.Count > 0)
      {
        var rewardProducts = normalizedProductIds
          .Select(productId => new LoyaltyRewardProduct
          {
            RewardId = reward.Id,
            ProductId = productId,
            DateCreated = DateTime.UtcNow
          });

        db.LoyaltyRewardProducts.AddRange(rewardProducts);
      }

      await db.SaveChangesAsync(ct);

      _logger.LogInformation("Updated loyalty reward {RewardId}: {Name}", reward.Id, reward.Name);

      return await MapToDto(db, reward, ct);
    }

    public async Task DeleteRewardAsync(Guid id, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      await using var db = _dbFactory.Create();

      var reward = await db.LoyaltyRewards.FindAsync(new object[] { id }, ct);
      if (reward == null)
        throw new KeyNotFoundException($"Reward {id} not found");

      var rewardProducts = await db.LoyaltyRewardProducts
        .Where(p => p.RewardId == id)
        .ToListAsync(ct);

      if (rewardProducts.Count > 0)
      {
        db.LoyaltyRewardProducts.RemoveRange(rewardProducts);
      }

      // Verificar si tiene canjes asociados
      var hasRedemptions = await db.LoyaltyRedemptions.AnyAsync(r => r.RewardId == id, ct);
      if (hasRedemptions)
      {
        // Soft delete: solo desactivar
        reward.IsActive = false;
        reward.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Deactivated loyalty reward {RewardId} (has redemptions)", id);
      }
      else
      {
        // Hard delete: eliminar
        db.LoyaltyRewards.Remove(reward);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted loyalty reward {RewardId}", id);
      }
    }

    public async Task<LoyaltyRewardDto?> GetRewardByIdAsync(Guid id, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      await using var db = _dbFactory.Create();

      var reward = await db.LoyaltyRewards.FindAsync(new object[] { id }, ct);
      if (reward == null)
        return null;

      return await MapToDto(db, reward, ct);
    }

    public async Task<PagedLoyaltyRewardsResponse> GetRewardsAsync(GetLoyaltyRewardsQuery query, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      await using var db = _dbFactory.Create();

      var rewardsQuery = db.LoyaltyRewards.AsNoTracking();

      // Filtros
      if (query.IsActive.HasValue)
        rewardsQuery = rewardsQuery.Where(r => r.IsActive == query.IsActive.Value);

      if (!string.IsNullOrWhiteSpace(query.RewardType))
        rewardsQuery = rewardsQuery.Where(r => r.RewardType == query.RewardType.ToUpper());

      if (!string.IsNullOrWhiteSpace(query.Search))
      {
        var search = query.Search.Trim();
        rewardsQuery = rewardsQuery.Where(r =>
            r.Name.Contains(search) ||
            (r.Description != null && r.Description.Contains(search)));
      }

      if (query.AvailableFrom.HasValue)
        rewardsQuery = rewardsQuery.Where(r => r.AvailableFrom.HasValue && r.AvailableFrom.Value >= query.AvailableFrom.Value);

      if (query.AvailableUntil.HasValue)
        rewardsQuery = rewardsQuery.Where(r => r.AvailableUntil.HasValue && r.AvailableUntil.Value <= query.AvailableUntil.Value);

      if (query.CreatedFrom.HasValue)
        rewardsQuery = rewardsQuery.Where(r => r.DateCreated >= query.CreatedFrom.Value);

      if (query.CreatedTo.HasValue)
        rewardsQuery = rewardsQuery.Where(r => r.DateCreated <= query.CreatedTo.Value);

      if (query.IsCurrentlyAvailable.HasValue)
      {
        var now = DateTime.UtcNow;
        if (query.IsCurrentlyAvailable.Value)
        {
          rewardsQuery = rewardsQuery.Where(r =>
              r.IsActive &&
              (!r.AvailableFrom.HasValue || r.AvailableFrom.Value <= now) &&
              (!r.AvailableUntil.HasValue || r.AvailableUntil.Value >= now));
        }
        else
        {
          rewardsQuery = rewardsQuery.Where(r =>
              !r.IsActive ||
              (r.AvailableFrom.HasValue && r.AvailableFrom.Value > now) ||
              (r.AvailableUntil.HasValue && r.AvailableUntil.Value < now));
        }
      }

      // Contar total
      var totalCount = await rewardsQuery.CountAsync(ct);

      // Paginación
      var pageSize = Math.Clamp(query.PageSize, 1, 100);
      var page = Math.Max(query.Page, 1);
      var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

      // Obtener resultados
      var rewards = await rewardsQuery
          .OrderBy(r => r.DisplayOrder)
          .ThenBy(r => r.Name)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync(ct);

      var dtos = new List<LoyaltyRewardDto>();
      foreach (var reward in rewards)
      {
        dtos.Add(await MapToDto(db, reward, ct));
      }

      return new PagedLoyaltyRewardsResponse(dtos, totalCount, page, pageSize, totalPages);
    }

    // ==================== REDEMPTIONS ====================

    public async Task<RedeemRewardResponse> RedeemRewardAsync(Guid userId, Guid rewardId, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      await using var db = _dbFactory.Create();

      var currentConfig = await db.LoyaltyConfigurations
          .AsNoTracking()
          .FirstOrDefaultAsync(ct);

      if (currentConfig != null && !currentConfig.IsEnabled)
      {
        throw new InvalidOperationException("Loyalty program is disabled for tenant");
      }

      // Obtener premio
      var reward = await db.LoyaltyRewards.FindAsync(new object[] { rewardId }, ct);
      if (reward == null)
        throw new KeyNotFoundException($"Reward {rewardId} not found");

      if (!reward.IsActive)
        throw new InvalidOperationException("This reward is no longer available");

      var now = DateTime.UtcNow;
      if (reward.AvailableFrom.HasValue && now < reward.AvailableFrom.Value)
        throw new InvalidOperationException("This reward is not available yet");

      if (reward.AvailableUntil.HasValue && now > reward.AvailableUntil.Value)
        throw new InvalidOperationException("This reward is no longer available");

      // Verificar stock
      if (reward.Stock.HasValue && reward.Stock.Value <= 0)
        throw new InvalidOperationException("This reward is out of stock");

      // Obtener cuenta de loyalty
      var account = await db.LoyaltyAccounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
      if (account == null)
        throw new InvalidOperationException("Loyalty account not found");

      // Verificar puntos suficientes
      if (account.PointsBalance < reward.PointsCost)
        throw new InvalidOperationException($"Insufficient points. You need {reward.PointsCost} points but have {account.PointsBalance}");

      // Crear canje
      var redeemedAt = DateTime.UtcNow;
      var redemption = new LoyaltyRedemption
      {
        Id = Guid.NewGuid(),
        LoyaltyAccountId = account.Id,
        RewardId = rewardId,
        PointsSpent = reward.PointsCost,
        Status = LoyaltyRedemptionStatus.Pending,
        RedeemedAt = redeemedAt,
        DateCreated = redeemedAt,
        UpdatedAt = redeemedAt
      };

      redemption.ExpiresAt = ResolveCouponExpirationDate(redeemedAt, reward.ValidityDays);

      // Generar cupón si es descuento
      if (reward.RewardType == LoyaltyRewardType.DiscountPercentage ||
          reward.RewardType == LoyaltyRewardType.DiscountFixed ||
          reward.RewardType == LoyaltyRewardType.FreeShipping)
      {
        redemption.CouponCode = GenerateCouponCode();
      }

      db.LoyaltyRedemptions.Add(redemption);

      // Descontar puntos
      account.PointsBalance -= reward.PointsCost;
      account.UpdatedAt = DateTime.UtcNow;

      // Crear transacción de canje
      var transaction = new LoyaltyTransaction
      {
        Id = Guid.NewGuid(),
        LoyaltyAccountId = account.Id,
        Type = LoyaltyTransactionType.Redeem,
        Points = -reward.PointsCost,
        Description = $"Redeemed: {reward.Name}",
        DateCreated = DateTime.UtcNow
      };
      db.LoyaltyTransactions.Add(transaction);

      // Reducir stock
      if (reward.Stock.HasValue)
      {
        reward.Stock--;
        reward.UpdatedAt = DateTime.UtcNow;
      }

      await db.SaveChangesAsync(ct);

      _logger.LogInformation(
          "User {UserId} redeemed reward {RewardId} for {Points} points",
          userId, rewardId, reward.PointsCost);

      return new RedeemRewardResponse(
          redemption.Id,
          $"Successfully redeemed {reward.Name}!",
          account.PointsBalance,
          redemption.CouponCode,
          redemption.ExpiresAt
      );
    }

    public async Task<LoyaltyRedemptionDto?> GetRedemptionByIdAsync(Guid id, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      await using var db = _dbFactory.Create();

      var redemption = await db.LoyaltyRedemptions
          .AsNoTracking()
          .FirstOrDefaultAsync(r => r.Id == id, ct);

      if (redemption == null)
        return null;

      return await MapRedemptionToDto(db, redemption, ct);
    }

    public async Task<PagedLoyaltyRedemptionsResponse> GetRedemptionsAsync(GetLoyaltyRedemptionsQuery query, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      await using var db = _dbFactory.Create();

      await ExpireRedemptionsByDateAsync(db, ct);

      var redemptionsQuery = db.LoyaltyRedemptions.AsNoTracking();

      // Filtros
      if (!string.IsNullOrWhiteSpace(query.Status))
        redemptionsQuery = redemptionsQuery.Where(r => r.Status == query.Status.ToUpper());

      if (query.UserId.HasValue)
      {
        var accountId = await db.LoyaltyAccounts
            .Where(a => a.UserId == query.UserId.Value)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (accountId != Guid.Empty)
          redemptionsQuery = redemptionsQuery.Where(r => r.LoyaltyAccountId == accountId);
      }

      if (!string.IsNullOrWhiteSpace(query.UserEmail))
      {
        var userEmail = query.UserEmail.Trim();

        var accountIds = await db.LoyaltyAccounts
            .Join(
              db.Users,
              account => account.UserId,
              user => user.Id,
              (account, user) => new { account.Id, user.Email })
            .Where(x => EF.Functions.ILike(x.Email, $"%{userEmail}%"))
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (accountIds.Count == 0)
        {
          return new PagedLoyaltyRedemptionsResponse(
            new List<LoyaltyRedemptionDto>(),
            0,
            Math.Max(query.Page, 1),
            Math.Clamp(query.PageSize, 1, 100),
            0);
        }

        redemptionsQuery = redemptionsQuery.Where(r => accountIds.Contains(r.LoyaltyAccountId));
      }

      if (query.FromDate.HasValue)
        redemptionsQuery = redemptionsQuery.Where(r => r.RedeemedAt >= query.FromDate.Value);

      if (query.ToDate.HasValue)
        redemptionsQuery = redemptionsQuery.Where(r => r.RedeemedAt <= query.ToDate.Value);

      // Contar total
      var totalCount = await redemptionsQuery.CountAsync(ct);

      // Paginación
      var pageSize = Math.Clamp(query.PageSize, 1, 100);
      var page = Math.Max(query.Page, 1);
      var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

      // Obtener resultados
      var redemptions = await redemptionsQuery
          .OrderByDescending(r => r.RedeemedAt)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync(ct);

      var dtos = new List<LoyaltyRedemptionDto>();
      foreach (var redemption in redemptions)
      {
        dtos.Add(await MapRedemptionToDto(db, redemption, ct));
      }

      return new PagedLoyaltyRedemptionsResponse(dtos, totalCount, page, pageSize, totalPages);
    }

    public async Task<LoyaltyRedemptionDto> UpdateRedemptionStatusAsync(Guid id, UpdateRedemptionStatusRequest request, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("No tenant context available");

      await using var db = _dbFactory.Create();

      var redemption = await db.LoyaltyRedemptions.FindAsync(new object[] { id }, ct);
      if (redemption == null)
        throw new KeyNotFoundException($"Redemption {id} not found");

      redemption.Status = request.Status.ToUpper();
      redemption.AdminNotes = request.AdminNotes;
      redemption.UpdatedAt = DateTime.UtcNow;

      if (request.Status.ToUpper() == LoyaltyRedemptionStatus.Delivered)
      {
        redemption.DeliveredAt = DateTime.UtcNow;
      }

      await db.SaveChangesAsync(ct);

      _logger.LogInformation("Updated redemption {RedemptionId} status to {Status}", id, request.Status);

      return await MapRedemptionToDto(db, redemption, ct);
    }

    // ==================== PRIVATE HELPERS ====================

    private async Task<LoyaltyRewardDto> MapToDto(TenantDbContext db, LoyaltyReward reward, CancellationToken ct)
    {
      string? productName = null;
      if (reward.ProductId.HasValue)
      {
        productName = await db.Products
            .Where(p => p.Id == reward.ProductId.Value)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);
      }

      var productIds = await db.LoyaltyRewardProducts
        .AsNoTracking()
        .Where(p => p.RewardId == reward.Id)
        .Select(p => p.ProductId)
        .ToListAsync(ct);

      var couponsIssued = await db.LoyaltyRedemptions
        .AsNoTracking()
        .CountAsync(r => r.RewardId == reward.Id, ct);

      int? couponsAvailable = null;
      if (reward.Stock.HasValue)
      {
        couponsAvailable = Math.Max(0, reward.Stock.Value);
      }

      var now = DateTime.UtcNow;
      var isCurrentlyAvailable =
        reward.IsActive &&
        (!reward.AvailableFrom.HasValue || reward.AvailableFrom.Value <= now) &&
        (!reward.AvailableUntil.HasValue || reward.AvailableUntil.Value >= now);

      return new LoyaltyRewardDto(
          reward.Id,
          reward.Name,
          reward.Description,
          reward.PointsCost,
          reward.RewardType,
          reward.ProductId,
          productIds,
          productName,
          reward.AppliesToAllEligibleProducts,
          reward.SingleProductSelectionRule,
          reward.DiscountValue,
          reward.ImageUrl,
          reward.IsActive,
          reward.Stock,
          reward.Stock,
          couponsIssued,
          couponsAvailable,
          reward.ValidityDays,
          reward.AvailableFrom,
          reward.AvailableUntil,
          isCurrentlyAvailable,
          reward.DisplayOrder,
          reward.DateCreated,
          reward.UpdatedAt
      );
    }

    private static int? ResolveStock(int? stock, int? couponQuantity)
    {
      if (stock.HasValue && couponQuantity.HasValue && stock.Value != couponQuantity.Value)
        throw new ArgumentException("Stock and CouponQuantity must match when both are provided");

      return couponQuantity ?? stock;
    }

    private static List<Guid> NormalizeProductIds(List<Guid>? productIds)
    {
      if (productIds == null || productIds.Count == 0)
      {
        return new List<Guid>();
      }

      return productIds
        .Where(id => id != Guid.Empty)
        .Distinct()
        .ToList();
    }

    private static Guid? ResolveProductIdForProductReward(string rewardType, List<Guid> productIds)
    {
      var normalizedType = rewardType.ToUpperInvariant();

      if (normalizedType != LoyaltyRewardType.Product)
      {
        return null;
      }

      if (productIds.Count > 1)
      {
        throw new ArgumentException("ProductIds must contain exactly one ProductId when RewardType is PRODUCT");
      }

      return productIds.Count == 1 ? productIds[0] : null;
    }

    private static void ValidateRewardTargeting(
      string rewardType,
      Guid? productId,
      List<Guid> productIds,
      decimal? discountValue,
      bool appliesToAllEligibleProducts,
      string? singleProductSelectionRule)
    {
      var normalizedType = rewardType.ToUpperInvariant();

      if (normalizedType == LoyaltyRewardType.Product)
      {
        if (!productId.HasValue || productId.Value == Guid.Empty)
          throw new ArgumentException("ProductIds must contain exactly one ProductId when RewardType is PRODUCT");

        if (productIds.Count > 1)
          throw new ArgumentException("ProductIds must contain exactly one ProductId when RewardType is PRODUCT");

        return;
      }

      if (normalizedType == LoyaltyRewardType.DiscountPercentage || normalizedType == LoyaltyRewardType.DiscountFixed)
      {
        if (!discountValue.HasValue || discountValue.Value <= 0)
          throw new ArgumentException("DiscountValue must be greater than 0 for discount rewards");

        if (!appliesToAllEligibleProducts)
        {
          var rule = singleProductSelectionRule?.ToUpperInvariant();
          if (!LoyaltySingleProductSelectionRule.IsValid(rule))
            throw new ArgumentException("SingleProductSelectionRule must be MOST_EXPENSIVE or CHEAPEST when AppliesToAllEligibleProducts is false");
        }

        return;
      }

      if (normalizedType == LoyaltyRewardType.FreeShipping)
      {
        if (!appliesToAllEligibleProducts)
        {
          var rule = singleProductSelectionRule?.ToUpperInvariant();
          if (!LoyaltySingleProductSelectionRule.IsValid(rule))
            throw new ArgumentException("SingleProductSelectionRule must be MOST_EXPENSIVE or CHEAPEST when AppliesToAllEligibleProducts is false");
        }

        return;
      }
    }

    private static void ValidateAvailabilityWindow(DateTime? availableFrom, DateTime? availableUntil)
    {
      if (availableFrom.HasValue && availableUntil.HasValue && availableFrom.Value > availableUntil.Value)
        throw new ArgumentException("AvailableFrom cannot be greater than AvailableUntil");
    }

    private async Task<LoyaltyRedemptionDto> MapRedemptionToDto(TenantDbContext db, LoyaltyRedemption redemption, CancellationToken ct)
    {
      var reward = await db.LoyaltyRewards
          .Where(r => r.Id == redemption.RewardId)
          .Select(r => new { r.Name, r.RewardType })
          .FirstOrDefaultAsync(ct);

      var redeemedBy = await db.LoyaltyAccounts
          .Where(a => a.Id == redemption.LoyaltyAccountId)
          .Join(
            db.Users,
            account => account.UserId,
            user => user.Id,
            (account, user) => new { account.UserId, user.Email })
          .FirstOrDefaultAsync(ct);

      string? orderNumber = null;
      if (redemption.OrderId.HasValue)
      {
        orderNumber = await db.Orders
            .Where(o => o.Id == redemption.OrderId.Value)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);
      }

      return new LoyaltyRedemptionDto(
          redemption.Id,
          redeemedBy?.UserId ?? Guid.Empty,
          redeemedBy?.Email,
          redemption.RewardId,
          reward?.Name ?? "Unknown",
          reward?.RewardType ?? "Unknown",
          redemption.PointsSpent,
          redemption.Status,
          redemption.CouponCode,
          redemption.RedeemedAt,
          redemption.ExpiresAt,
          redemption.DeliveredAt,
          redemption.AdminNotes,
          redemption.OrderId,
          orderNumber
      );
    }

    private string GenerateCouponCode()
    {
      const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      var random = new Random();
      return "LOYALTY-" + new string(Enumerable.Repeat(chars, 8)
          .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private static DateTime? ResolveCouponExpirationDate(DateTime redeemedAtUtc, int? validityDays)
    {
      if (!validityDays.HasValue)
      {
        return null;
      }

      if (validityDays.Value <= 0)
      {
        return null;
      }

      return redeemedAtUtc.Date.AddDays(validityDays.Value);
    }

    private static readonly string[] ExpirableRedemptionStatuses =
    {
      LoyaltyRedemptionStatus.Pending,
      LoyaltyRedemptionStatus.Approved
    };

    private async Task ExpireRedemptionsByDateAsync(TenantDbContext db, CancellationToken ct)
    {
      var todayUtcDate = DateTime.UtcNow.Date;

      var redemptionsToExpire = await db.LoyaltyRedemptions
        .Where(r =>
          r.ExpiresAt.HasValue &&
          r.ExpiresAt.Value.Date < todayUtcDate &&
          ExpirableRedemptionStatuses.Contains(r.Status))
        .ToListAsync(ct);

      if (redemptionsToExpire.Count == 0)
      {
        return;
      }

      foreach (var redemption in redemptionsToExpire)
      {
        redemption.Status = LoyaltyRedemptionStatus.Expired;
        redemption.UpdatedAt = DateTime.UtcNow;
      }

      await db.SaveChangesAsync(ct);

      _logger.LogInformation(
        "Auto-expired {Count} loyalty redemptions by date for tenant {TenantSlug}",
        redemptionsToExpire.Count,
        _tenantAccessor.TenantInfo?.Slug);
    }
  }
}
