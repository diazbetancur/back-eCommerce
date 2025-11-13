using CC.Aplication.Catalog;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Services
{
    public interface ICheckoutService
    {
        Task<CheckoutQuoteResponse> GetQuoteAsync(string sessionId, CheckoutQuoteRequest request, CancellationToken ct = default);
        Task<PlaceOrderResponse> PlaceOrderAsync(string sessionId, PlaceOrderRequest request, Guid? userId = null, CancellationToken ct = default);
    }

    public class CheckoutService : ICheckoutService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly IConfiguration _configuration;
        private readonly IFeatureService _featureService;
        private readonly ILogger<CheckoutService> _logger;

        public CheckoutService(
            TenantDbContextFactory dbFactory,
            IConfiguration configuration,
            IFeatureService featureService,
            ILogger<CheckoutService> logger)
        {
            _dbFactory = dbFactory;
            _configuration = configuration;
            _featureService = featureService;
            _logger = logger;
        }

        public async Task<CheckoutQuoteResponse> GetQuoteAsync(string sessionId, CheckoutQuoteRequest request, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                throw new InvalidOperationException("Cart is empty");
            }

            // Calcular subtotal
            var items = new List<CartItemDto>();
            decimal subtotal = 0;

            foreach (var item in cart.Items)
            {
                var product = await db.Products.FindAsync(new object[] { item.ProductId }, ct);
                if (product == null || !product.IsActive)
                {
                    throw new InvalidOperationException($"Product {item.ProductId} is not available");
                }

                if (product.Stock < item.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for {product.Name}. Available: {product.Stock}");
                }

                var itemSubtotal = item.Price * item.Quantity;
                subtotal += itemSubtotal;

                items.Add(new CartItemDto
                {
                    Id = item.Id,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    Subtotal = itemSubtotal
                });
            }

            // Obtener tasa de impuesto de settings
            var taxRateSetting = await db.Settings.FirstOrDefaultAsync(s => s.Key == "TaxRate", ct);
            var taxRate = taxRateSetting != null ? decimal.Parse(taxRateSetting.Value) : 0.15m;

            var tax = subtotal * taxRate;
            var shipping = CalculateShipping(subtotal);
            var total = subtotal + tax + shipping;

            return new CheckoutQuoteResponse
            {
                Subtotal = subtotal,
                Tax = tax,
                Shipping = shipping,
                Total = total,
                Items = items
            };
        }

        public async Task<PlaceOrderResponse> PlaceOrderAsync(string sessionId, PlaceOrderRequest request, Guid? userId = null, CancellationToken ct = default)
        {
            // Verificar si el checkout como guest está permitido
            if (!userId.HasValue)
            {
                var allowGuestCheckout = await _featureService.IsEnabledAsync("AllowGuestCheckout", ct);
                if (!allowGuestCheckout)
                {
                    throw new InvalidOperationException("Guest checkout is not allowed for this tenant. Please sign in.");
                }
            }

            await using var db = _dbFactory.Create();

            // Verificar idempotencia
            var existingOrder = await db.Orders
                .FirstOrDefaultAsync(o => o.IdempotencyKey == request.IdempotencyKey, ct);

            if (existingOrder != null)
            {
                _logger.LogWarning("Order already exists with idempotency key: {IdempotencyKey}", request.IdempotencyKey);
                
                return new PlaceOrderResponse
                {
                    OrderId = existingOrder.Id,
                    OrderNumber = existingOrder.OrderNumber,
                    Total = existingOrder.Total,
                    Status = existingOrder.Status,
                    CreatedAt = existingOrder.CreatedAt
                };
            }

            // Obtener carrito
            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                throw new InvalidOperationException("Cart is empty");
            }

            // Verificar límite de items del carrito (feature flag)
            var maxCartItems = await _featureService.GetValueAsync("MaxCartItems", 100, ct);
            var totalItems = cart.Items.Sum(i => i.Quantity);
            if (totalItems > maxCartItems)
            {
                throw new InvalidOperationException($"Cart exceeds maximum allowed items ({maxCartItems})");
            }

            // Calcular totales
            decimal subtotal = 0;
            var orderItems = new List<OrderItem>();

            foreach (var item in cart.Items)
            {
                var product = await db.Products.FindAsync(new object[] { item.ProductId }, ct);
                if (product == null || !product.IsActive)
                {
                    throw new InvalidOperationException($"Product {item.ProductId} is not available");
                }

                if (product.Stock < item.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient stock for {product.Name}. Available: {product.Stock}");
                }

                var itemSubtotal = item.Price * item.Quantity;
                subtotal += itemSubtotal;

                orderItems.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    Subtotal = itemSubtotal
                });

                // Reducir stock
                product.Stock -= item.Quantity;
            }

            // Calcular impuesto y envío
            var taxRateSetting = await db.Settings.FirstOrDefaultAsync(s => s.Key == "TaxRate", ct);
            var taxRate = taxRateSetting != null ? decimal.Parse(taxRateSetting.Value) : 0.15m;
            
            var tax = subtotal * taxRate;
            var shipping = CalculateShipping(subtotal);
            var total = subtotal + tax + shipping;

            // Crear orden
            var orderNumber = GenerateOrderNumber();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = orderNumber,
                UserId = userId,
                SessionId = sessionId,
                IdempotencyKey = request.IdempotencyKey,
                Total = total,
                Subtotal = subtotal,
                Tax = tax,
                Shipping = shipping,
                Status = "PENDING",
                ShippingAddress = request.ShippingAddress,
                Email = request.Email,
                Phone = request.Phone,
                PaymentMethod = request.PaymentMethod,
                CreatedAt = DateTime.UtcNow
            };

            db.Orders.Add(order);

            // Agregar items a la orden
            foreach (var orderItem in orderItems)
            {
                orderItem.OrderId = order.Id;
                db.OrderItems.Add(orderItem);
            }

            // Limpiar carrito
            db.CartItems.RemoveRange(cart.Items);

            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Order placed: {OrderId} - {OrderNumber}. Total: {Total}", 
                order.Id, order.OrderNumber, order.Total);

            return new PlaceOrderResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Total = order.Total,
                Status = order.Status,
                CreatedAt = order.CreatedAt
            };
        }

        private decimal CalculateShipping(decimal subtotal)
        {
            // Envío gratis sobre $100
            if (subtotal >= 100)
                return 0;

            // Envío fijo $10
            return 10m;
        }

        private string GenerateOrderNumber()
        {
            // Formato: ORD-YYYYMMDD-XXXXXX
            var date = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random().Next(100000, 999999);
            return $"ORD-{date}-{random}";
        }
    }
}
