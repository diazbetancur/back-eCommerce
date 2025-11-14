using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Orders
{
    public interface IOrderService
    {
        Task<PagedOrdersResponse> GetUserOrdersAsync(Guid userId, GetOrdersQuery query, CancellationToken ct = default);
        Task<OrderDetailDto?> GetOrderDetailAsync(Guid userId, Guid orderId, CancellationToken ct = default);
    }

    public class OrderService : IOrderService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor,
            ILogger<OrderService> logger)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
            _logger = logger;
        }

        public async Task<PagedOrdersResponse> GetUserOrdersAsync(
            Guid userId, 
            GetOrdersQuery query, 
            CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Query base filtrada por usuario
            var ordersQuery = db.Orders
                .Where(o => o.UserId == userId)
                .AsNoTracking();

            // Aplicar filtros opcionales
            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                ordersQuery = ordersQuery.Where(o => o.Status == query.Status.ToUpper());
            }

            if (query.FromDate.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.CreatedAt >= query.FromDate.Value);
            }

            if (query.ToDate.HasValue)
            {
                ordersQuery = ordersQuery.Where(o => o.CreatedAt <= query.ToDate.Value);
            }

            // Contar total
            var totalCount = await ordersQuery.CountAsync(ct);

            // Calcular paginación
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Obtener órdenes paginadas
            var orders = await ordersQuery
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.Status,
                    o.Total,
                    o.CreatedAt,
                    ItemCount = db.OrderItems.Count(i => i.OrderId == o.Id)
                })
                .ToListAsync(ct);

            var orderDtos = orders.Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.Total,
                o.CreatedAt,
                o.ItemCount
            )).ToList();

            return new PagedOrdersResponse(
                orderDtos,
                totalCount,
                page,
                pageSize,
                totalPages
            );
        }

        public async Task<OrderDetailDto?> GetOrderDetailAsync(
            Guid userId, 
            Guid orderId, 
            CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Buscar orden que pertenezca al usuario
            var order = await db.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);

            if (order == null)
            {
                return null;
            }

            // Obtener items de la orden
            var items = await db.OrderItems
                .Where(i => i.OrderId == orderId)
                .AsNoTracking()
                .Select(i => new OrderItemDetailDto(
                    i.Id,
                    i.ProductId,
                    i.ProductName,
                    i.Quantity,
                    i.Price,
                    i.Subtotal
                ))
                .ToListAsync(ct);

            return new OrderDetailDto(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.Total,
                order.Subtotal,
                order.Tax,
                order.Shipping,
                order.ShippingAddress,
                order.Email,
                order.Phone,
                order.PaymentMethod,
                order.CreatedAt,
                order.CompletedAt,
                items
            );
        }
    }
}
