using CC.Domain.Loyalty;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Loyalty
{
    public interface ILoyaltyService
    {
        Task<LoyaltyAccountSummaryDto> GetUserLoyaltyAsync(Guid userId, CancellationToken ct = default);
        Task<PagedLoyaltyTransactionsResponse> GetUserTransactionsAsync(Guid userId, GetLoyaltyTransactionsQuery query, CancellationToken ct = default);
        Task<int> AddPointsForOrderAsync(Guid userId, Guid orderId, decimal orderTotal, CancellationToken ct = default);
        Task<LoyaltyConfig> GetLoyaltyConfigAsync(CancellationToken ct = default);
    }

    public class LoyaltyService : ILoyaltyService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly ILogger<LoyaltyService> _logger;

        public LoyaltyService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor,
            ILogger<LoyaltyService> logger)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
            _logger = logger;
        }

        public async Task<LoyaltyAccountSummaryDto> GetUserLoyaltyAsync(Guid userId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Obtener o crear cuenta de loyalty
            var account = await GetOrCreateAccountAsync(db, userId, ct);

            // Calcular totales
            var transactions = await db.LoyaltyTransactions
                .Where(t => t.LoyaltyAccountId == account.Id)
                .AsNoTracking()
                .ToListAsync(ct);

            var totalEarned = transactions
                .Where(t => t.Type == LoyaltyTransactionType.Earn)
                .Sum(t => t.Points);

            var totalRedeemed = Math.Abs(transactions
                .Where(t => t.Type == LoyaltyTransactionType.Redeem)
                .Sum(t => t.Points));

            // Últimas 5 transacciones
            var lastTransactions = await db.LoyaltyTransactions
                .Where(t => t.LoyaltyAccountId == account.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Select(t => new
                {
                    t.Id,
                    t.Type,
                    t.Points,
                    t.Description,
                    t.OrderId,
                    t.CreatedAt
                })
                .AsNoTracking()
                .ToListAsync(ct);

            var lastTransactionDtos = new List<LoyaltyTransactionDto>();
            foreach (var tx in lastTransactions)
            {
                string? orderNumber = null;
                if (tx.OrderId.HasValue)
                {
                    var order = await db.Orders
                        .Where(o => o.Id == tx.OrderId.Value)
                        .Select(o => o.OrderNumber)
                        .FirstOrDefaultAsync(ct);
                    orderNumber = order;
                }

                lastTransactionDtos.Add(new LoyaltyTransactionDto(
                    tx.Id,
                    tx.Type,
                    tx.Points,
                    tx.Description,
                    orderNumber,
                    tx.CreatedAt
                ));
            }

            return new LoyaltyAccountSummaryDto(
                account.PointsBalance,
                totalEarned,
                totalRedeemed,
                lastTransactionDtos
            );
        }

        public async Task<PagedLoyaltyTransactionsResponse> GetUserTransactionsAsync(
            Guid userId, 
            GetLoyaltyTransactionsQuery query, 
            CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Obtener cuenta
            var account = await db.LoyaltyAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == userId, ct);

            if (account == null)
            {
                return new PagedLoyaltyTransactionsResponse(
                    new List<LoyaltyTransactionDto>(),
                    0, 1, query.PageSize, 0
                );
            }

            // Query base
            var transactionsQuery = db.LoyaltyTransactions
                .Where(t => t.LoyaltyAccountId == account.Id)
                .AsNoTracking();

            // Filtros
            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                transactionsQuery = transactionsQuery.Where(t => t.Type == query.Type.ToUpper());
            }

            if (query.FromDate.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(t => t.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(t => t.CreatedAt <= query.ToDate.Value);
            }

            // Contar total
            var totalCount = await transactionsQuery.CountAsync(ct);

            // Paginación
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Obtener transacciones
            var transactions = await transactionsQuery
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.Type,
                    t.Points,
                    t.Description,
                    t.OrderId,
                    t.CreatedAt
                })
                .ToListAsync(ct);

            var transactionDtos = new List<LoyaltyTransactionDto>();
            foreach (var tx in transactions)
            {
                string? orderNumber = null;
                if (tx.OrderId.HasValue)
                {
                    var order = await db.Orders
                        .Where(o => o.Id == tx.OrderId.Value)
                        .Select(o => o.OrderNumber)
                        .FirstOrDefaultAsync(ct);
                    orderNumber = order;
                }

                transactionDtos.Add(new LoyaltyTransactionDto(
                    tx.Id,
                    tx.Type,
                    tx.Points,
                    tx.Description,
                    orderNumber,
                    tx.CreatedAt
                ));
            }

            return new PagedLoyaltyTransactionsResponse(
                transactionDtos,
                totalCount,
                page,
                pageSize,
                totalPages
            );
        }

        public async Task<int> AddPointsForOrderAsync(
            Guid userId, 
            Guid orderId, 
            decimal orderTotal, 
            CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            // Obtener configuración de loyalty
            var config = await GetLoyaltyConfigAsync(ct);
            if (!config.Enabled)
            {
                _logger.LogInformation("Loyalty program is disabled for tenant");
                return 0;
            }

            // Calcular puntos ganados
            var earnedPoints = CalculatePoints(orderTotal, config);
            if (earnedPoints <= 0)
            {
                _logger.LogInformation("No points earned for order {OrderId} (total: {Total})", orderId, orderTotal);
                return 0;
            }

            await using var db = _dbFactory.Create();

            // Obtener o crear cuenta
            var account = await GetOrCreateAccountAsync(db, userId, ct);

            // Verificar que no exista ya una transacción para esta orden
            var existingTransaction = await db.LoyaltyTransactions
                .FirstOrDefaultAsync(t => t.OrderId == orderId, ct);

            if (existingTransaction != null)
            {
                _logger.LogWarning("Points already awarded for order {OrderId}", orderId);
                return 0;
            }

            // Crear transacción
            var transaction = new LoyaltyTransaction
            {
                Id = Guid.NewGuid(),
                LoyaltyAccountId = account.Id,
                OrderId = orderId,
                Type = LoyaltyTransactionType.Earn,
                Points = earnedPoints,
                Description = $"Points earned from order (${orderTotal:F2})",
                CreatedAt = DateTime.UtcNow
            };

            db.LoyaltyTransactions.Add(transaction);

            // Actualizar balance
            account.PointsBalance += earnedPoints;
            account.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Awarded {Points} points to user {UserId} for order {OrderId}",
                earnedPoints, userId, orderId);

            return earnedPoints;
        }

        public async Task<LoyaltyConfig> GetLoyaltyConfigAsync(CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Buscar configuración en Settings
            var loyaltyEnabledSetting = await db.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "LoyaltyEnabled", ct);

            var pointsPerUnitSetting = await db.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "LoyaltyPointsPerUnit", ct);

            var currencyUnitSetting = await db.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "LoyaltyCurrencyUnit", ct);

            // Valores por defecto
            var enabled = loyaltyEnabledSetting != null && bool.Parse(loyaltyEnabledSetting.Value);
            var pointsPerUnit = pointsPerUnitSetting != null ? int.Parse(pointsPerUnitSetting.Value) : 1;
            var currencyUnit = currencyUnitSetting != null ? decimal.Parse(currencyUnitSetting.Value) : 1000m;

            return new LoyaltyConfig(enabled, pointsPerUnit, currencyUnit);
        }

        // ==================== PRIVATE HELPERS ====================

        private async Task<LoyaltyAccount> GetOrCreateAccountAsync(
            TenantDbContext db, 
            Guid userId, 
            CancellationToken ct)
        {
            var account = await db.LoyaltyAccounts
                .FirstOrDefaultAsync(a => a.UserId == userId, ct);

            if (account == null)
            {
                account = new LoyaltyAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PointsBalance = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.LoyaltyAccounts.Add(account);
                await db.SaveChangesAsync(ct);

                _logger.LogInformation("Created loyalty account for user {UserId}", userId);
            }

            return account;
        }

        private int CalculatePoints(decimal orderTotal, LoyaltyConfig config)
        {
            // Formula: floor(orderTotal / currencyUnit) * pointsPerUnit
            // Ej: $5000 / $1000 = 5 unidades * 1 punto/unidad = 5 puntos
            var units = Math.Floor(orderTotal / config.CurrencyUnit);
            var points = (int)(units * config.PointsPerCurrencyUnit);
            return points;
        }
    }
}
