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
        List<LoyaltyTransactionDto> LastTransactions
    );

    public record LoyaltyTransactionDto(
        Guid Id,
        string Type,              // EARN, REDEEM, ADJUST
        int Points,               // Positivo para earn, negativo para redeem
        string? Description,
        string? OrderNumber,      // Si está asociado a una orden
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
        int PageSize = 20,
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
}
