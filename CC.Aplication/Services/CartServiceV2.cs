using CC.Aplication.Catalog;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Services
{
  /// <summary>
  /// V2 Cart Service using Unit of Work pattern.
  /// Demonstrates the new standardized approach.
  /// </summary>
  public class CartServiceV2 : ICartService
  {
    private readonly ITenantUnitOfWorkFactory _uowFactory;
    private readonly ILogger<CartServiceV2> _logger;

    public CartServiceV2(ITenantUnitOfWorkFactory uowFactory, ILogger<CartServiceV2> logger)
    {
      _uowFactory = uowFactory;
      _logger = logger;
    }

    public async Task<CartDto> GetOrCreateCartAsync(string sessionId, Guid? userId = null, CancellationToken ct = default)
    {
      await using var uow = _uowFactory.Create();

      var cart = await uow.Carts.Query
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

        uow.Carts.Add(cart);
        await uow.SaveChangesAsync(ct);

        _logger.LogInformation("Cart created: {CartId} for session {SessionId}", cart.Id, sessionId);
      }

      return await MapCartToDtoAsync(uow, cart, ct);
    }

    public async Task<CartDto> AddToCartAsync(string sessionId, AddToCartRequest request, Guid? userId = null, CancellationToken ct = default)
    {
      await using var uow = _uowFactory.Create();

      // Verificar producto
      var product = await uow.Products.GetByIdAsync(request.ProductId, ct);
      if (product == null || !product.IsActive)
      {
        throw new InvalidOperationException("Product not found or inactive");
      }

      if (product.Stock < request.Quantity)
      {
        throw new InvalidOperationException($"Insufficient stock. Available: {product.Stock}");
      }

      // Obtener o crear carrito
      var cart = await uow.Carts.Query
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
        uow.Carts.Add(cart);
      }

      // Buscar item existente
      var existingItem = cart.Items?.FirstOrDefault(i => i.ProductId == request.ProductId);

      if (existingItem != null)
      {
        existingItem.Quantity += request.Quantity;
        existingItem.Price = product.Price;
        uow.CartItems.Update(existingItem);
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
        uow.CartItems.Add(newItem);
      }

      cart.UpdatedAt = DateTime.UtcNow;
      await uow.SaveChangesAsync(ct);

      _logger.LogInformation("Product {ProductId} added to cart {CartId}. Quantity: {Quantity}",
          request.ProductId, cart.Id, request.Quantity);

      return await MapCartToDtoAsync(uow, cart, ct);
    }

    public async Task<CartDto> UpdateCartItemAsync(string sessionId, Guid cartItemId, UpdateCartItemRequest request, CancellationToken ct = default)
    {
      await using var uow = _uowFactory.Create();

      var cart = await uow.Carts.Query
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
      var product = await uow.Products.GetByIdAsync(item.ProductId, ct);
      if (product == null || !product.IsActive)
      {
        throw new InvalidOperationException("Product not found or inactive");
      }

      if (product.Stock < request.Quantity)
      {
        throw new InvalidOperationException($"Insufficient stock. Available: {product.Stock}");
      }

      item.Quantity = request.Quantity;
      uow.CartItems.Update(item);

      cart.UpdatedAt = DateTime.UtcNow;
      await uow.SaveChangesAsync(ct);

      _logger.LogInformation("Cart item {ItemId} updated. New quantity: {Quantity}", cartItemId, request.Quantity);

      return await MapCartToDtoAsync(uow, cart, ct);
    }

    public async Task<bool> RemoveCartItemAsync(string sessionId, Guid cartItemId, CancellationToken ct = default)
    {
      await using var uow = _uowFactory.Create();

      var cart = await uow.Carts.Query
          .Include(c => c.Items)
          .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

      if (cart == null)
        return false;

      var item = cart.Items?.FirstOrDefault(i => i.Id == cartItemId);
      if (item == null)
        return false;

      uow.CartItems.Remove(item);
      cart.UpdatedAt = DateTime.UtcNow;
      await uow.SaveChangesAsync(ct);

      _logger.LogInformation("Cart item {ItemId} removed from cart {CartId}", cartItemId, cart.Id);

      return true;
    }

    public async Task<bool> ClearCartAsync(string sessionId, CancellationToken ct = default)
    {
      await using var uow = _uowFactory.Create();

      var cart = await uow.Carts.Query
          .Include(c => c.Items)
          .FirstOrDefaultAsync(c => c.SessionId == sessionId, ct);

      if (cart == null)
        return false;

      if (cart.Items != null && cart.Items.Any())
      {
        uow.CartItems.RemoveRange(cart.Items);
      }

      cart.UpdatedAt = DateTime.UtcNow;
      await uow.SaveChangesAsync(ct);

      _logger.LogInformation("Cart {CartId} cleared", cart.Id);

      return true;
    }

    private async Task<CartDto> MapCartToDtoAsync(ITenantUnitOfWork uow, Cart cart, CancellationToken ct)
    {
      // Using LINQ join through DbContext for complex queries
      var items = await uow.CartItems.Query
          .Where(ci => ci.CartId == cart.Id)
          .Join(uow.Products.Query,
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
