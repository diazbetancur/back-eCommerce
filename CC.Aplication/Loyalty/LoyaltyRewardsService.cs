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

      await using var db = _dbFactory.Create();

      var reward = new LoyaltyReward
      {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Description = request.Description,
        PointsCost = request.PointsCost,
        RewardType = request.RewardType.ToUpper(),
        ProductId = request.ProductId,
        DiscountValue = request.DiscountValue,
        ImageUrl = request.ImageUrl,
        IsActive = request.IsActive,
        Stock = request.Stock,
        ValidityDays = request.ValidityDays,
        DisplayOrder = request.DisplayOrder,
        DateCreated = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };

      db.LoyaltyRewards.Add(reward);
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

      await using var db = _dbFactory.Create();

      var reward = await db.LoyaltyRewards.FindAsync(new object[] { id }, ct);
      if (reward == null)
        throw new KeyNotFoundException($"Reward {id} not found");

      reward.Name = request.Name;
      reward.Description = request.Description;
      reward.PointsCost = request.PointsCost;
      reward.RewardType = request.RewardType.ToUpper();
      reward.ProductId = request.ProductId;
      reward.DiscountValue = request.DiscountValue;
      reward.ImageUrl = request.ImageUrl;
      reward.IsActive = request.IsActive;
      reward.Stock = request.Stock;
      reward.ValidityDays = request.ValidityDays;
      reward.DisplayOrder = request.DisplayOrder;
      reward.UpdatedAt = DateTime.UtcNow;

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

      // Obtener premio
      var reward = await db.LoyaltyRewards.FindAsync(new object[] { rewardId }, ct);
      if (reward == null)
        throw new KeyNotFoundException($"Reward {rewardId} not found");

      if (!reward.IsActive)
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
      var redemption = new LoyaltyRedemption
      {
        Id = Guid.NewGuid(),
        LoyaltyAccountId = account.Id,
        RewardId = rewardId,
        PointsSpent = reward.PointsCost,
        Status = LoyaltyRedemptionStatus.Pending,
        RedeemedAt = DateTime.UtcNow,
        DateCreated = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };

      // Generar cupón si es descuento
      if (reward.RewardType == LoyaltyRewardType.DiscountPercentage ||
          reward.RewardType == LoyaltyRewardType.DiscountFixed ||
          reward.RewardType == LoyaltyRewardType.FreeShipping)
      {
        redemption.CouponCode = GenerateCouponCode();
        if (reward.ValidityDays.HasValue)
        {
          redemption.ExpiresAt = DateTime.UtcNow.AddDays(reward.ValidityDays.Value);
        }
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

      return new LoyaltyRewardDto(
          reward.Id,
          reward.Name,
          reward.Description,
          reward.PointsCost,
          reward.RewardType,
          reward.ProductId,
          productName,
          reward.DiscountValue,
          reward.ImageUrl,
          reward.IsActive,
          reward.Stock,
          reward.ValidityDays,
          reward.DisplayOrder,
          reward.DateCreated,
          reward.UpdatedAt
      );
    }

    private async Task<LoyaltyRedemptionDto> MapRedemptionToDto(TenantDbContext db, LoyaltyRedemption redemption, CancellationToken ct)
    {
      var reward = await db.LoyaltyRewards
          .Where(r => r.Id == redemption.RewardId)
          .Select(r => new { r.Name, r.RewardType })
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
  }
}
