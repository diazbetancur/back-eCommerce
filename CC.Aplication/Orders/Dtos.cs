using CC.Aplication.Catalog;

namespace CC.Aplication.Orders
{
    // ==================== SUMMARY DTOs ====================
    
    public record OrderSummaryDto(
        Guid Id,
        string OrderNumber,
        string Status,
        decimal Total,
        DateTime CreatedAt,
        int ItemCount
    );

    public record PagedOrdersResponse(
        List<OrderSummaryDto> Items,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    // ==================== DETAIL DTOs ====================

    public record OrderDetailDto(
        Guid Id,
        string OrderNumber,
        string Status,
        decimal Total,
        decimal Subtotal,
        decimal Tax,
        decimal Shipping,
        string ShippingAddress,
        string Email,
        string? Phone,
        string PaymentMethod,
        DateTime CreatedAt,
        DateTime? CompletedAt,
        List<OrderItemDetailDto> Items
    );

    public record OrderItemDetailDto(
        Guid Id,
        Guid ProductId,
        string ProductName,
        int Quantity,
        decimal Price,
        decimal Subtotal
    );

    // ==================== QUERY PARAMETERS ====================

    public record GetOrdersQuery(
        int Page = 1,
        int PageSize = 20,
        string? Status = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null
    );
}
