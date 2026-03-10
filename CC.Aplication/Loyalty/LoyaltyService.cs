using CC.Domain.Entities;
using CC.Domain.Enums;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CC.Aplication.Loyalty
{
    public interface ILoyaltyService
    {
        Task<LoyaltyAccountSummaryDto> GetUserLoyaltyAsync(Guid userId, CancellationToken ct = default);
        Task<PagedLoyaltyTransactionsResponse> GetUserTransactionsAsync(Guid userId, GetLoyaltyTransactionsQuery query, CancellationToken ct = default);
        Task<int> AddPointsForOrderAsync(Guid userId, Guid orderId, decimal orderTotal, CancellationToken ct = default);
        Task<LoyaltyConfig> GetLoyaltyConfigAsync(CancellationToken ct = default);
        Task<AdjustPointsResponse> AdjustPointsManuallyAsync(AdjustPointsRequest request, Guid adjustedByUserId, CancellationToken ct = default);
        Task<PagedManualPointAdjustmentsResponse> GetManualPointAdjustmentsAsync(GetManualPointAdjustmentsQuery query, CancellationToken ct = default);
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

            // Procesar vencimientos pendientes antes de calcular dashboard
            await ExpireEligiblePointsAsync(db, account, ct);

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

            var pointsExpiringIn60Days = CalculatePointsExpiringInDays(transactions, DateTime.UtcNow, 60);

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
                    t.ExpiresAt,
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
                    BuildMovementDetail(tx.Type, tx.Points),
                    tx.Points,
                    tx.DateCreated,
                    ResolveExpirationDate(tx.Type, tx.DateCreated, tx.ExpiresAt),
                    tx.Description,
                    orderNumber,
                    tx.DateCreated
                ));
            }

            return new LoyaltyAccountSummaryDto(
                account.PointsBalance,
                totalEarned,
                totalRedeemed,
                pointsExpiringIn60Days,
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
                .FirstOrDefaultAsync(a => a.UserId == userId, ct);

            if (account == null)
            {
                return new PagedLoyaltyTransactionsResponse(
                    new List<LoyaltyTransactionDto>(),
                    0, 1, query.PageSize, 0
                );
            }

            // Procesar vencimientos pendientes antes del extracto
            await ExpireEligiblePointsAsync(db, account, ct);

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
                    t.ExpiresAt,
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
                    BuildMovementDetail(tx.Type, tx.Points),
                    tx.Points,
                    tx.DateCreated,
                    ResolveExpirationDate(tx.Type, tx.DateCreated, tx.ExpiresAt),
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

        public async Task<AdjustPointsResponse> AdjustPointsManuallyAsync(AdjustPointsRequest request, Guid adjustedByUserId, CancellationToken ct = default)
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

            var currentConfig = await GetOrCreateConfigurationAsync(db, ct);
            if (!currentConfig.IsEnabled)
            {
                throw new InvalidOperationException("Loyalty program is disabled for tenant");
            }

            ValidateManualAdjustmentPoints(request.Points);

            // Obtener o crear cuenta
            var account = await GetOrCreateAccountAsync(db, request.UserId, ct);

            // Ajustar puntos según tipo
            int pointsToAdd = request.Points;
            if (request.TransactionType == LoyaltyTransactionType.Redeem && request.Points > 0)
            {
                pointsToAdd = -request.Points; // Redeem siempre resta
            }

            var now = DateTime.UtcNow;
            var expiresAt = ResolveManualAdjustmentExpiration(
                pointsToAdd,
                currentConfig.PointsExpirationDays,
                now);

            // Validar que no quede en negativo
            var newBalance = account.PointsBalance + pointsToAdd;
            if (newBalance < 0)
            {
                throw new InvalidOperationException($"Cannot adjust points. User balance would be negative ({newBalance})");
            }

            // Crear transacción
            var transactionId = Guid.NewGuid();
            var description = $"Manual adjustment: {request.Reason}";
            var transaction = new LoyaltyTransaction
            {
                Id = transactionId,
                LoyaltyAccountId = account.Id,
                Type = request.TransactionType,
                Points = pointsToAdd,
                Description = description,
                AdjustedByUserId = adjustedByUserId,
                AdjustmentTicketNumber = NormalizeTicketNumber(request.TicketNumber),
                ExpiresAt = expiresAt,
                DateCreated = now
            };

            try
            {
                db.LoyaltyTransactions.Add(transaction);

                // Actualizar balance
                account.PointsBalance = newBalance;
                account.UpdatedAt = now;

                await db.SaveChangesAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                _logger.LogWarning(ex,
                    "Tenant DB schema is missing manual adjustment metadata columns. Falling back to legacy insert for tenant {TenantSlug}",
                    _tenantAccessor.TenantInfo?.Slug);

                db.ChangeTracker.Clear();

                var fallback = await ExecuteLegacyManualAdjustmentWithoutMetadataAsync(
                    db,
                    request.UserId,
                    request.TransactionType,
                    pointsToAdd,
                    description,
                    expiresAt,
                    now,
                    ct);

                transactionId = fallback.TransactionId;
                newBalance = fallback.NewBalance;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "42703")
            {
                _logger.LogWarning(ex,
                    "Tenant DB schema is missing manual adjustment metadata columns (wrapped DbUpdateException). Falling back to legacy insert for tenant {TenantSlug}",
                    _tenantAccessor.TenantInfo?.Slug);

                db.ChangeTracker.Clear();

                var fallback = await ExecuteLegacyManualAdjustmentWithoutMetadataAsync(
                    db,
                    request.UserId,
                    request.TransactionType,
                    pointsToAdd,
                    description,
                    expiresAt,
                    now,
                    ct);

                transactionId = fallback.TransactionId;
                newBalance = fallback.NewBalance;
            }

            _logger.LogInformation(
                "Manual points adjustment for user {UserId}: {Points} points ({Type}). Reason: {Reason}",
                request.UserId, pointsToAdd, request.TransactionType, request.Reason);

            return new AdjustPointsResponse(
                transactionId,
                pointsToAdd,
                newBalance,
                $"Points adjusted successfully. New balance: {newBalance} points"
            );
        }

        private async Task<LegacyAdjustResult> ExecuteLegacyManualAdjustmentWithoutMetadataAsync(
            TenantDbContext db,
            Guid userId,
            string transactionType,
            int pointsToAdd,
            string description,
            DateTime? expiresAt,
            DateTime now,
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
                    DateCreated = now,
                    UpdatedAt = now
                };

                db.LoyaltyAccounts.Add(account);
                await db.SaveChangesAsync(ct);
            }

            var newBalance = account.PointsBalance + pointsToAdd;
            if (newBalance < 0)
            {
                throw new InvalidOperationException($"Cannot adjust points. User balance would be negative ({newBalance})");
            }

            var transactionId = Guid.NewGuid();

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.Database.ExecuteSqlInterpolatedAsync(
                     $@"INSERT INTO ""LoyaltyTransactions""
                         (""Id"", ""LoyaltyAccountId"", ""Type"", ""Points"", ""Description"", ""ExpiresAt"", ""DateCreated"")
                   VALUES
                   ({transactionId}, {account.Id}, {transactionType}, {pointsToAdd}, {description}, {expiresAt}, {now})", ct);

            account.PointsBalance = newBalance;
            account.UpdatedAt = now;
            await db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            return new LegacyAdjustResult(transactionId, newBalance);
        }

        public async Task<PagedManualPointAdjustmentsResponse> GetManualPointAdjustmentsAsync(
            GetManualPointAdjustmentsQuery query,
            CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            try
            {
                return await ExecuteManualAdjustmentsQueryWithMetadataAsync(db, query, ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "42703")
            {
                _logger.LogWarning(ex,
                    "Tenant DB schema is missing manual adjustment metadata columns. Falling back to legacy query for tenant {TenantSlug}",
                    _tenantAccessor.TenantInfo?.Slug);

                return await ExecuteManualAdjustmentsLegacyQueryAsync(db, query, ct);
            }
        }

        private static IQueryable<ManualAdjustmentProjection> BuildManualAdjustmentsQueryWithMetadata(TenantDbContext db)
        {
            return
                from tx in db.LoyaltyTransactions.AsNoTracking()
                join account in db.LoyaltyAccounts.AsNoTracking() on tx.LoyaltyAccountId equals account.Id
                join customer in db.Users.AsNoTracking() on account.UserId equals customer.Id
                join admin in db.Users.AsNoTracking() on tx.AdjustedByUserId equals admin.Id into adminJoin
                from admin in adminJoin.DefaultIfEmpty()
                where tx.Description != null && EF.Functions.ILike(tx.Description!, "Manual adjustment:%")
                select new ManualAdjustmentProjection
                {
                    TransactionId = tx.Id,
                    UserId = account.UserId,
                    UserEmail = customer.Email,
                    AdjustedByUserId = tx.AdjustedByUserId,
                    AdjustedByEmail = admin != null ? admin.Email : null,
                    Points = tx.Points,
                    TransactionType = tx.Type,
                    ObservationsSource = tx.Description,
                    TicketNumber = tx.AdjustmentTicketNumber,
                    ExpiresAt = tx.ExpiresAt,
                    CreatedAt = tx.DateCreated
                };
        }

        private async Task<PagedManualPointAdjustmentsResponse> ExecuteManualAdjustmentsQueryWithMetadataAsync(
            TenantDbContext db,
            GetManualPointAdjustmentsQuery query,
            CancellationToken ct)
        {
            var manualAdjustmentsQuery = BuildManualAdjustmentsQueryWithMetadata(db);

            if (query.UserId.HasValue)
            {
                manualAdjustmentsQuery = manualAdjustmentsQuery.Where(x => x.UserId == query.UserId.Value);
            }

            if (query.AdjustedByUserId.HasValue)
            {
                manualAdjustmentsQuery = manualAdjustmentsQuery.Where(x => x.AdjustedByUserId == query.AdjustedByUserId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.TicketNumber))
            {
                var ticketNumber = query.TicketNumber.Trim();
                manualAdjustmentsQuery = manualAdjustmentsQuery.Where(x =>
                    x.TicketNumber != null && EF.Functions.ILike(x.TicketNumber, $"%{ticketNumber}%"));
            }

            if (query.FromDate.HasValue)
            {
                manualAdjustmentsQuery = manualAdjustmentsQuery.Where(x => x.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                manualAdjustmentsQuery = manualAdjustmentsQuery.Where(x => x.CreatedAt <= query.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                manualAdjustmentsQuery = manualAdjustmentsQuery.Where(x =>
                    EF.Functions.ILike(x.UserEmail, $"%{search}%") ||
                    (x.AdjustedByEmail != null && EF.Functions.ILike(x.AdjustedByEmail, $"%{search}%")) ||
                    (x.TicketNumber != null && EF.Functions.ILike(x.TicketNumber, $"%{search}%")) ||
                    (x.ObservationsSource != null && EF.Functions.ILike(x.ObservationsSource, $"%{search}%")));
            }

            var totalCount = await manualAdjustmentsQuery.CountAsync(ct);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await manualAdjustmentsQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var mappedItems = items.Select(x => new ManualPointAdjustmentItemDto(
                x.TransactionId,
                x.UserId,
                x.UserEmail,
                x.AdjustedByUserId,
                x.AdjustedByEmail,
                x.Points,
                x.TransactionType,
                ExtractManualObservations(x.ObservationsSource),
                x.TicketNumber,
                x.ExpiresAt,
                x.CreatedAt
            )).ToList();

            return new PagedManualPointAdjustmentsResponse(
                mappedItems,
                totalCount,
                page,
                pageSize,
                totalPages
            );
        }

        private async Task<PagedManualPointAdjustmentsResponse> ExecuteManualAdjustmentsLegacyQueryAsync(
            TenantDbContext db,
            GetManualPointAdjustmentsQuery query,
            CancellationToken ct)
        {
            var legacyQuery =
                from tx in db.LoyaltyTransactions.AsNoTracking()
                join account in db.LoyaltyAccounts.AsNoTracking() on tx.LoyaltyAccountId equals account.Id
                join customer in db.Users.AsNoTracking() on account.UserId equals customer.Id
                where tx.Description != null && EF.Functions.ILike(tx.Description!, "Manual adjustment:%")
                select new LegacyManualAdjustmentProjection
                {
                    TransactionId = tx.Id,
                    UserId = account.UserId,
                    UserEmail = customer.Email,
                    Points = tx.Points,
                    TransactionType = tx.Type,
                    ObservationsSource = tx.Description,
                    ExpiresAt = tx.ExpiresAt,
                    CreatedAt = tx.DateCreated
                };

            if (query.UserId.HasValue)
            {
                legacyQuery = legacyQuery.Where(x => x.UserId == query.UserId.Value);
            }

            if (query.FromDate.HasValue)
            {
                legacyQuery = legacyQuery.Where(x => x.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                legacyQuery = legacyQuery.Where(x => x.CreatedAt <= query.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                legacyQuery = legacyQuery.Where(x =>
                    EF.Functions.ILike(x.UserEmail, $"%{search}%") ||
                    (x.ObservationsSource != null && EF.Functions.ILike(x.ObservationsSource, $"%{search}%")));
            }

            var totalCount = await legacyQuery.CountAsync(ct);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await legacyQuery
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var mappedItems = items.Select(x => new ManualPointAdjustmentItemDto(
                x.TransactionId,
                x.UserId,
                x.UserEmail,
                null,
                null,
                x.Points,
                x.TransactionType,
                ExtractManualObservations(x.ObservationsSource),
                null,
                x.ExpiresAt,
                x.CreatedAt
            )).ToList();

            return new PagedManualPointAdjustmentsResponse(
                mappedItems,
                totalCount,
                page,
                pageSize,
                totalPages
            );
        }

        // ==================== PRIVATE HELPERS ====================

        private static void ValidateManualAdjustmentPoints(int points)
        {
            if (points == 0)
            {
                throw new ArgumentException("Points must be different from 0");
            }
        }

        private static string? NormalizeTicketNumber(string? ticketNumber)
        {
            if (string.IsNullOrWhiteSpace(ticketNumber))
            {
                return null;
            }

            return ticketNumber.Trim();
        }

        private static string? ExtractManualObservations(string? description)
        {
            const string prefix = "Manual adjustment:";
            if (string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            if (!description.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return description;
            }

            return description.Substring(prefix.Length).Trim();
        }

        private static DateTime? ResolveManualAdjustmentExpiration(
            int pointsToAdd,
            int? pointsExpirationDays,
            DateTime now)
        {
            if (pointsToAdd <= 0)
            {
                return null;
            }

            if (!pointsExpirationDays.HasValue)
            {
                return null;
            }

            return now.AddDays(pointsExpirationDays.Value);
        }

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

        private async Task<int> ExpireEligiblePointsAsync(
            TenantDbContext db,
            LoyaltyAccount account,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var transactions = await db.LoyaltyTransactions
                .Where(t => t.LoyaltyAccountId == account.Id)
                .OrderBy(t => t.DateCreated)
                .ThenBy(t => t.Id)
                .ToListAsync(ct);

            if (!transactions.Any())
            {
                return 0;
            }

            var buckets = BuildEarnBuckets(transactions);

            var expiredBuckets = buckets
                .Where(b => b.ExpiresAt.HasValue && b.ExpiresAt.Value <= now && b.RemainingPoints > 0)
                .ToList();

            if (!expiredBuckets.Any())
            {
                return 0;
            }

            var expiredTotal = 0;

            foreach (var bucket in expiredBuckets)
            {
                var pointsToExpire = bucket.RemainingPoints;
                expiredTotal += pointsToExpire;

                var expirationTransaction = new LoyaltyTransaction
                {
                    Id = Guid.NewGuid(),
                    LoyaltyAccountId = account.Id,
                    Type = LoyaltyTransactionType.Expire,
                    Points = -pointsToExpire,
                    Description = $"Points expired from transaction {bucket.SourceTransactionId}",
                    ExpiresAt = bucket.ExpiresAt,
                    DateCreated = bucket.TransactionDate
                };

                db.LoyaltyTransactions.Add(expirationTransaction);
            }

            account.PointsBalance = Math.Max(0, account.PointsBalance - expiredTotal);
            account.UpdatedAt = now;

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Expired {ExpiredPoints} loyalty points for account {AccountId}",
                expiredTotal,
                account.Id);

            return expiredTotal;
        }

        private static int CalculatePointsExpiringInDays(
            List<LoyaltyTransaction> transactions,
            DateTime now,
            int days)
        {
            var buckets = BuildEarnBuckets(transactions);
            var threshold = now.AddDays(days);

            return buckets
                .Where(b =>
                    b.ExpiresAt.HasValue &&
                    b.ExpiresAt.Value > now &&
                    b.ExpiresAt.Value <= threshold &&
                    b.RemainingPoints > 0)
                .Sum(b => b.RemainingPoints);
        }

        private static List<EarnBucket> BuildEarnBuckets(List<LoyaltyTransaction> transactions)
        {
            var earnBuckets = transactions
                .Where(t => t.Type == LoyaltyTransactionType.Earn && t.Points > 0)
                .OrderBy(t => t.DateCreated)
                .ThenBy(t => t.Id)
                .Select(t => new EarnBucket(t.Id, t.DateCreated, t.ExpiresAt, t.Points))
                .ToList();

            var totalNegativePoints = transactions
                .Where(t => t.Points < 0)
                .Sum(t => -t.Points);

            var remainingToConsume = totalNegativePoints;

            foreach (var bucket in earnBuckets)
            {
                if (remainingToConsume <= 0)
                {
                    break;
                }

                var consumed = Math.Min(bucket.RemainingPoints, remainingToConsume);
                bucket.RemainingPoints -= consumed;
                remainingToConsume -= consumed;
            }

            return earnBuckets;
        }

        private static DateTime? ResolveExpirationDate(string type, DateTime transactionDate, DateTime? expiresAt)
        {
            if (type == LoyaltyTransactionType.Redeem)
            {
                return transactionDate;
            }

            return expiresAt;
        }

        private static string BuildMovementDetail(string type, int points)
        {
            return type switch
            {
                LoyaltyTransactionType.Earn => "Acumulación",
                LoyaltyTransactionType.Redeem => "Redención",
                LoyaltyTransactionType.Expire => "Vencimiento",
                LoyaltyTransactionType.Adjust when points >= 0 => "Ajuste (+)",
                LoyaltyTransactionType.Adjust => "Ajuste (-)",
                _ => "Movimiento"
            };
        }

        private sealed class EarnBucket
        {
            public EarnBucket(Guid sourceTransactionId, DateTime transactionDate, DateTime? expiresAt, int remainingPoints)
            {
                SourceTransactionId = sourceTransactionId;
                TransactionDate = transactionDate;
                ExpiresAt = expiresAt;
                RemainingPoints = remainingPoints;
            }

            public Guid SourceTransactionId { get; }
            public DateTime TransactionDate { get; }
            public DateTime? ExpiresAt { get; }
            public int RemainingPoints { get; set; }
        }

        private sealed class ManualAdjustmentProjection
        {
            public Guid TransactionId { get; set; }
            public Guid UserId { get; set; }
            public string UserEmail { get; set; } = string.Empty;
            public Guid? AdjustedByUserId { get; set; }
            public string? AdjustedByEmail { get; set; }
            public int Points { get; set; }
            public string TransactionType { get; set; } = string.Empty;
            public string? ObservationsSource { get; set; }
            public string? TicketNumber { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed class LegacyManualAdjustmentProjection
        {
            public Guid TransactionId { get; set; }
            public Guid UserId { get; set; }
            public string UserEmail { get; set; } = string.Empty;
            public int Points { get; set; }
            public string TransactionType { get; set; } = string.Empty;
            public string? ObservationsSource { get; set; }
            public DateTime? ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed class LegacyAdjustResult
        {
            public LegacyAdjustResult(Guid transactionId, int newBalance)
            {
                TransactionId = transactionId;
                NewBalance = newBalance;
            }

            public Guid TransactionId { get; }
            public int NewBalance { get; }
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
