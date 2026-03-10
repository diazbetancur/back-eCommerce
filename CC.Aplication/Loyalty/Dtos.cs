namespace CC.Aplication.Loyalty
{
    // ==================== RESPONSE DTOs ====================

    public record LoyaltyAccountDto(
        Guid Id,
        int Balance,
        int TotalEarned,
        int TotalRedeemed,
        DateTime UpdatedAt
    );

    public record LoyaltyAccountSummaryDto(
        int Balance,
        int TotalEarned,
        int TotalRedeemed,
        int PointsExpiringIn60Days,
        List<LoyaltyTransactionDto> LastTransactions
    );

    public record LoyaltyTransactionDto(
        Guid Id,
        string Type,              // EARN, REDEEM, ADJUST
        string Detail,            // Acumulación, Redención, Vencimiento, Ajuste
        int Points,               // Positivo para earn, negativo para redeem
        DateTime TransactionDate, // Fecha de compra/redención. En vencimiento: fecha de compra
        DateTime? ExpirationDate, // En redención: mismo día de redención
        string? Description,
        string? OrderNumber,      // Si est� asociado a una orden
        DateTime CreatedAt
    );

    public record PagedLoyaltyTransactionsResponse(
        List<LoyaltyTransactionDto> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    // ==================== QUERY PARAMETERS ====================

    public record GetLoyaltyTransactionsQuery(
        int Page = 1,
        int PageSize = 50,
        string? Type = null,       // Filtrar por tipo
        DateTime? FromDate = null,
        DateTime? ToDate = null
    );

    // ==================== CONFIGURATION ====================

    public record LoyaltyConfig(
        bool Enabled,
        int PointsPerCurrencyUnit,
        decimal CurrencyUnit
    );

    // ==================== CONFIGURATION DTOs ====================

    /// <summary>
    /// Configuración del programa de lealtad del tenant
    /// </summary>
    public record LoyaltyConfigDto(
        Guid Id,
        decimal ConversionRate,
        int? PointsExpirationDays,
        bool IsEnabled,
        decimal? MinPurchaseForPoints,
        DateTime CreatedAt,
        DateTime? UpdatedAt
    );

    /// <summary>
    /// Request para actualizar la configuración de lealtad
    /// </summary>
    public record UpdateLoyaltyConfigRequest(
        decimal ConversionRate,
        int? PointsExpirationDays,
        bool IsEnabled,
        decimal? MinPurchaseForPoints
    );
}
