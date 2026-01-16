using CC.Domain.Entities;
using CC.Domain.Enums;
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
        Task<AdjustPointsResponse> AdjustPointsManuallyAsync(AdjustPointsRequest request, CancellationToken ct = default);
        Task<LoyaltyConfigDto> GetLoyaltyConfigurationAsync(CancellationToken ct = default);
        Task<LoyaltyConfigDto> UpdateLoyaltyConfigurationAsync(UpdateLoyaltyConfigRequest request, CancellationToken ct = default);
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

            // �ltimas 5 transacciones
            var lastTransactions = await db.LoyaltyTransactions
                .Where(t => t.LoyaltyAccountId == account.Id)
                .OrderByDescending(t => t.DateCreated)
                .Take(5)
                .Select(t => new
                {
                    t.Id,
                    t.Type,
                    t.Points,
                    t.Description,
                    t.OrderId,
                    t.DateCreated
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
                    tx.DateCreated
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
                transactionsQuery = transactionsQuery.Where(t => t.DateCreated >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                transactionsQuery = transactionsQuery.Where(t => t.DateCreated <= query.ToDate.Value);
            }

            // Contar total
            var totalCount = await transactionsQuery.CountAsync(ct);

            // Paginaci�n
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Obtener transacciones
            var transactions = await transactionsQuery
                .OrderByDescending(t => t.DateCreated)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.Type,
                    t.Points,
                    t.Description,
                    t.OrderId,
                    t.DateCreated
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
                    tx.DateCreated
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

            await using var db = _dbFactory.Create();

            // Obtener configuraci�n actual
            var currentConfig = await GetOrCreateConfigurationAsync(db, ct);

            if (!currentConfig.IsEnabled)
            {
                _logger.LogInformation("Loyalty program is disabled for tenant");
                return 0;
            }

            // Validar monto mínimo si está configurado
            if (currentConfig.MinPurchaseForPoints.HasValue &&
                orderTotal < currentConfig.MinPurchaseForPoints.Value)
            {
                _logger.LogInformation(
                    "Order total {Total} is below minimum purchase {Min} for points",
                    orderTotal, currentConfig.MinPurchaseForPoints.Value);
                return 0;
            }

            // Calcular puntos ganados usando la tasa de conversión actual
            var earnedPoints = (int)Math.Floor(orderTotal * currentConfig.ConversionRate);
            if (earnedPoints <= 0)
            {
                _logger.LogInformation("No points earned for order {OrderId} (total: {Total})", orderId, orderTotal);
                return 0;
            }

            // Obtener o crear cuenta
            var account = await GetOrCreateAccountAsync(db, userId, ct);

            // Verificar que no exista ya una transacci�n para esta orden
            var existingTransaction = await db.LoyaltyTransactions
                .FirstOrDefaultAsync(t => t.OrderId == orderId, ct);

            if (existingTransaction != null)
            {
                _logger.LogWarning("Points already awarded for order {OrderId}", orderId);
                return 0;
            }

            // Calcular fecha de expiración si está configurado
            DateTime? expiresAt = null;
            if (currentConfig.PointsExpirationDays.HasValue)
            {
                expiresAt = DateTime.UtcNow.AddDays(currentConfig.PointsExpirationDays.Value);
            }

            // Crear transacci�n con snapshot de configuración
            var transaction = new LoyaltyTransaction
            {
                Id = Guid.NewGuid(),
                LoyaltyAccountId = account.Id,
                OrderId = orderId,
                Type = LoyaltyTransactionType.Earn,
                Points = earnedPoints,
                Description = $"Points earned from order (${orderTotal:F2})",
                ConversionRateUsed = currentConfig.ConversionRate,
                ExpiresAt = expiresAt,
                DateCreated = DateTime.UtcNow
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

            // Buscar configuraci�n en Settings
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

        public async Task<AdjustPointsResponse> AdjustPointsManuallyAsync(AdjustPointsRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            // Validar tipo de transacción
            if (request.TransactionType != LoyaltyTransactionType.Earn &&
                request.TransactionType != LoyaltyTransactionType.Redeem &&
                request.TransactionType != LoyaltyTransactionType.Adjust)
            {
                throw new ArgumentException("Invalid transaction type. Use EARN, REDEEM, or ADJUST");
            }

            await using var db = _dbFactory.Create();

            // Obtener o crear cuenta
            var account = await GetOrCreateAccountAsync(db, request.UserId, ct);

            // Ajustar puntos según tipo
            int pointsToAdd = request.Points;
            if (request.TransactionType == LoyaltyTransactionType.Redeem && request.Points > 0)
            {
                pointsToAdd = -request.Points; // Redeem siempre resta
            }

            // Validar que no quede en negativo
            var newBalance = account.PointsBalance + pointsToAdd;
            if (newBalance < 0)
            {
                throw new InvalidOperationException($"Cannot adjust points. User balance would be negative ({newBalance})");
            }

            // Crear transacción
            var transaction = new LoyaltyTransaction
            {
                Id = Guid.NewGuid(),
                LoyaltyAccountId = account.Id,
                Type = request.TransactionType,
                Points = pointsToAdd,
                Description = $"Manual adjustment: {request.Reason}",
                DateCreated = DateTime.UtcNow
            };

            db.LoyaltyTransactions.Add(transaction);

            // Actualizar balance
            account.PointsBalance = newBalance;
            account.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Manual points adjustment for user {UserId}: {Points} points ({Type}). Reason: {Reason}",
                request.UserId, pointsToAdd, request.TransactionType, request.Reason);

            return new AdjustPointsResponse(
                transaction.Id,
                pointsToAdd,
                newBalance,
                $"Points adjusted successfully. New balance: {newBalance} points"
            );
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
                    DateCreated = DateTime.UtcNow,
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

        private async Task<LoyaltyConfiguration> GetOrCreateConfigurationAsync(
            TenantDbContext db,
            CancellationToken ct)
        {
            var config = await db.LoyaltyConfigurations
                .FirstOrDefaultAsync(ct);

            if (config == null)
            {
                config = new LoyaltyConfiguration
                {
                    Id = Guid.NewGuid(),
                    ConversionRate = 10m, // Default: 10 puntos por $1
                    PointsExpirationDays = null, // Sin vencimiento por defecto
                    IsEnabled = true,
                    MinPurchaseForPoints = null,
                    DateCreated = DateTime.UtcNow
                };

                db.LoyaltyConfigurations.Add(config);
                await db.SaveChangesAsync(ct);

                _logger.LogInformation("Created default loyalty configuration");
            }

            return config;
        }

        // ==================== CONFIGURATION METHODS ====================

        public async Task<LoyaltyConfigDto> GetLoyaltyConfigurationAsync(CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            var config = await GetOrCreateConfigurationAsync(db, ct);

            return new LoyaltyConfigDto(
                config.Id,
                config.ConversionRate,
                config.PointsExpirationDays,
                config.IsEnabled,
                config.MinPurchaseForPoints,
                config.DateCreated,
                config.UpdatedAt
            );
        }

        public async Task<LoyaltyConfigDto> UpdateLoyaltyConfigurationAsync(
            UpdateLoyaltyConfigRequest request,
            CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            // Validaciones
            if (request.ConversionRate <= 0)
            {
                throw new ArgumentException("Conversion rate must be greater than 0");
            }

            if (request.PointsExpirationDays.HasValue && request.PointsExpirationDays.Value <= 0)
            {
                throw new ArgumentException("Points expiration days must be greater than 0 or null");
            }

            if (request.MinPurchaseForPoints.HasValue && request.MinPurchaseForPoints.Value < 0)
            {
                throw new ArgumentException("Minimum purchase for points cannot be negative");
            }

            await using var db = _dbFactory.Create();

            var config = await GetOrCreateConfigurationAsync(db, ct);

            // Actualizar configuración
            config.ConversionRate = request.ConversionRate;
            config.PointsExpirationDays = request.PointsExpirationDays;
            config.IsEnabled = request.IsEnabled;
            config.MinPurchaseForPoints = request.MinPurchaseForPoints;
            config.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Updated loyalty configuration: ConversionRate={Rate}, ExpirationDays={Days}, Enabled={Enabled}",
                config.ConversionRate, config.PointsExpirationDays, config.IsEnabled);

            return new LoyaltyConfigDto(
                config.Id,
                config.ConversionRate,
                config.PointsExpirationDays,
                config.IsEnabled,
                config.MinPurchaseForPoints,
                config.DateCreated,
                config.UpdatedAt
            );
        }
    }
}
