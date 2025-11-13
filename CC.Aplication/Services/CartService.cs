using CC.Aplication.Catalog;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Services
{
    public interface ICartService
    {
        Task<CartDto> GetOrCreateCartAsync(string sessionId, Guid? userId = null, CancellationToken ct = default);
        Task<CartDto> AddToCartAsync(string sessionId, AddToCartRequest request, Guid? userId = null, CancellationToken ct = default);
        Task<CartDto> UpdateCartItemAsync(string sessionId, Guid cartItemId, UpdateCartItemRequest request, CancellationToken ct = default);
        Task<bool> RemoveCartItemAsync(string sessionId, Guid cartItemId, CancellationToken ct = default);
        Task<bool> ClearCartAsync(string sessionId, CancellationToken ct = default);
    }

    public class CartService : ICartService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ILogger<CartService> _logger;

        public CartService(TenantDbContextFactory dbFactory, ILogger<CartService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<CartDto> GetOrCreateCartAsync(string sessionId, Guid? userId = null, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId || (userId.HasValue && c.UserId == userId.Value), ct);

            if (cart == null)
            {
                cart = new Cart
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.Carts.Add(cart);
                await db.SaveChangesAsync(ct);

                _logger.LogInformation("Cart created: {CartId} for session {SessionId}", cart.Id, sessionId);
            }

            return await MapCartToDtoAsync(db, cart, ct);
        }

        public async Task<CartDto> AddToCartAsync(string sessionId, AddToCartRequest request, Guid? userId = null, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            // Verificar producto
            var product = await db.Products.FindAsync(new object[] { request.ProductId }, ct);
            if (product == null || !product.IsActive)
            {
                throw new InvalidOperationException("Product not found or inactive");
            }

            if (product.Stock < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock. Available: {product.Stock}");
            }

            // Obtener o crear carrito
            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId || (userId.HasValue && c.UserId == userId.Value), ct);

            if (cart == null)
            {
                cart = new Cart
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.Carts.Add(cart);
            }

            // Buscar item existente
            var existingItem = cart.Items?.FirstOrDefault(i => i.ProductId == request.ProductId);
            
            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
                existingItem.Price = product.Price; // Actualizar precio
            }
            else
            {
                var newItem = new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = cart.Id,
                    ProductId = product.Id,
                    Quantity = request.Quantity,
                    Price = product.Price,
                    AddedAt = DateTime.UtcNow
                };
                db.CartItems.Add(newItem);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Product {ProductId} added to cart {CartId}. Quantity: {Quantity}", 
                request.ProductId, cart.Id, request.Quantity);

            return await MapCartToDtoAsync(db, cart, ct);
        }

        public async Task<CartDto> UpdateCartItemAsync(string sessionId, Guid cartItemId, UpdateCartItemRequest request, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

            if (cart == null)
            {
                throw new InvalidOperationException("Cart not found");
            }

            var item = cart.Items?.FirstOrDefault(i => i.Id == cartItemId);
            if (item == null)
            {
                throw new InvalidOperationException("Cart item not found");
            }

            // Verificar stock
            var product = await db.Products.FindAsync(new object[] { item.ProductId }, ct);
            if (product == null || !product.IsActive)
            {
                throw new InvalidOperationException("Product not found or inactive");
            }

            if (product.Stock < request.Quantity)
            {
                throw new InvalidOperationException($"Insufficient stock. Available: {product.Stock}");
            }

            item.Quantity = request.Quantity;
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Cart item {ItemId} updated. New quantity: {Quantity}", cartItemId, request.Quantity);

            return await MapCartToDtoAsync(db, cart, ct);
        }

        public async Task<bool> RemoveCartItemAsync(string sessionId, Guid cartItemId, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

            if (cart == null)
                return false;

            var item = cart.Items?.FirstOrDefault(i => i.Id == cartItemId);
            if (item == null)
                return false;

            db.CartItems.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Cart item {ItemId} removed from cart {CartId}", cartItemId, cart.Id);

            return true;
        }

        public async Task<bool> ClearCartAsync(string sessionId, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var cart = await db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

            if (cart == null)
                return false;

            if (cart.Items != null && cart.Items.Any())
            {
                db.CartItems.RemoveRange(cart.Items);
            }

            cart.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Cart {CartId} cleared", cart.Id);

            return true;
        }

        private async Task<CartDto> MapCartToDtoAsync(TenantDbContext db, Cart cart, CancellationToken ct)
        {
            var items = await db.CartItems
                .Where(ci => ci.CartId == cart.Id)
                .Join(db.Products,
                    ci => ci.ProductId,
                    p => p.Id,
                    (ci, p) => new CartItemDto
                    {
                        Id = ci.Id,
                        ProductId = p.Id,
                        ProductName = p.Name,
                        Price = ci.Price,
                        Quantity = ci.Quantity,
                        Subtotal = ci.Price * ci.Quantity
                    })
                .ToListAsync(ct);

            return new CartDto
            {
                Id = cart.Id,
                Items = items,
                Subtotal = items.Sum(i => i.Subtotal),
                TotalItems = items.Sum(i => i.Quantity)
            };
        }
    }
}
