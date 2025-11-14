namespace CC.Aplication.Favorites
{
    // ==================== REQUEST DTOs ====================

    public record AddFavoriteRequest(
        Guid ProductId
    );

    // ==================== RESPONSE DTOs ====================

    public record FavoriteProductDto(
        Guid ProductId,
        string ProductName,
        decimal Price,
        string? MainImageUrl,
        DateTime AddedAt,
        bool IsActive  // Si el producto sigue activo
    );

    public record FavoriteListResponse(
        List<FavoriteProductDto> Items,
        int TotalCount
    );

    public record AddFavoriteResponse(
        Guid FavoriteId,
        Guid ProductId,
        string Message
    );
}
