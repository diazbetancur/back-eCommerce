using CC.Domain.Dto;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Aplication.Stores
{
  public interface IStockService
  {
    /// <summary>
    /// Verifica stock disponible con compatibilidad hacia atrás.
    /// Si storeId es null, usa Product.Stock (legacy).
    /// Si storeId es proporcionado, usa ProductStoreStock.
    /// </summary>
    Task<StockAvailabilityResponse> CheckStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el stock disponible de un producto (backward compatible)
    /// </summary>
    Task<int> GetAvailableStockAsync(Guid productId, Guid? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Reserva stock de un producto (backward compatible)
    /// </summary>
    Task ReserveStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Libera stock reservado (por si una orden se cancela)
    /// </summary>
    Task ReleaseStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Decrementa el stock después de completar una venta
    /// </summary>
    Task DecrementStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el stock de un producto en todas las tiendas
    /// </summary>
    Task<List<ProductStoreStockDto>> GetProductStockByStoresAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Actualiza el stock de un producto en una tienda específica
    /// </summary>
    Task<ProductStoreStockDto> UpdateProductStoreStockAsync(Guid productId, UpdateProductStoreStockRequest request, CancellationToken ct = default);

    /// <summary>
    /// Migra el stock legacy (Product.Stock) a una tienda específica
    /// </summary>
    Task<int> MigrateAllLegacyStockToStoreAsync(Guid targetStoreId, CancellationToken ct = default);
  }

  public class StockService : IStockService
  {
    private readonly TenantDbContextFactory _dbFactory;

    public StockService(TenantDbContextFactory dbFactory)
    {
      _dbFactory = dbFactory;
    }

    public async Task<StockAvailabilityResponse> CheckStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default)
    {
      var availableStock = await GetAvailableStockAsync(productId, storeId, ct);
      var isAvailable = availableStock >= quantity;

      return new StockAvailabilityResponse
      {
        IsAvailable = isAvailable,
        AvailableStock = availableStock,
        StoreId = storeId,
        UsedLegacyStock = storeId == null,
        Message = isAvailable
              ? $"Stock available: {availableStock}"
              : $"Insufficient stock. Available: {availableStock}, Required: {quantity}"
      };
    }

    public async Task<int> GetAvailableStockAsync(Guid productId, Guid? storeId = null, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      // MODO LEGACY: Si no se especifica tienda, usar Product.Stock
      if (storeId == null)
      {
        var product = await db.Products.FindAsync(new object[] { productId }, ct);
        if (product == null)
        {
          throw new InvalidOperationException($"Product with ID '{productId}' not found");
        }

        return product.Stock;
      }

      // MODO STORES: Buscar stock en la tienda específica
      var storeStock = await db.ProductStoreStock
          .FirstOrDefaultAsync(pss => pss.ProductId == productId && pss.StoreId == storeId.Value, ct);

      if (storeStock == null)
      {
        // Si no existe registro de stock para esa tienda, retornar 0
        return 0;
      }

      // Stock disponible = Stock total - Stock reservado
      return storeStock.Stock - storeStock.ReservedStock;
    }

    public async Task ReserveStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default)
    {
      if (quantity <= 0)
      {
        throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
      }

      await using var db = _dbFactory.Create();

      // MODO LEGACY: No reservamos en Product.Stock (simplemente verificamos disponibilidad)
      if (storeId == null)
      {
        var product = await db.Products.FindAsync(new object[] { productId }, ct);
        if (product == null)
        {
          throw new InvalidOperationException($"Product with ID '{productId}' not found");
        }

        if (product.Stock < quantity)
        {
          throw new InvalidOperationException($"Insufficient stock. Available: {product.Stock}, Required: {quantity}");
        }

        // En modo legacy no tenemos ReservedStock, la validación es suficiente
        return;
      }

      // MODO STORES: Reservar stock en la tienda específica
      var storeStock = await db.ProductStoreStock
          .FirstOrDefaultAsync(pss => pss.ProductId == productId && pss.StoreId == storeId.Value, ct);

      if (storeStock == null)
      {
        throw new InvalidOperationException($"No stock record found for product '{productId}' in store '{storeId}'");
      }

      var availableStock = storeStock.Stock - storeStock.ReservedStock;
      if (availableStock < quantity)
      {
        throw new InvalidOperationException($"Insufficient stock. Available: {availableStock}, Required: {quantity}");
      }

      storeStock.ReservedStock += quantity;
      storeStock.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);
    }

    public async Task ReleaseStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default)
    {
      if (quantity <= 0)
      {
        throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
      }

      await using var db = _dbFactory.Create();

      // MODO LEGACY: No hay reservas, no hacer nada
      if (storeId == null)
      {
        return;
      }

      // MODO STORES: Liberar stock reservado
      var storeStock = await db.ProductStoreStock
          .FirstOrDefaultAsync(pss => pss.ProductId == productId && pss.StoreId == storeId.Value, ct);

      if (storeStock == null)
      {
        throw new InvalidOperationException($"No stock record found for product '{productId}' in store '{storeId}'");
      }

      storeStock.ReservedStock = Math.Max(0, storeStock.ReservedStock - quantity);
      storeStock.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);
    }

    public async Task DecrementStockAsync(Guid productId, int quantity, Guid? storeId = null, CancellationToken ct = default)
    {
      if (quantity <= 0)
      {
        throw new ArgumentException("Quantity must be greater than 0", nameof(quantity));
      }

      await using var db = _dbFactory.Create();

      // MODO LEGACY: Decrementar Product.Stock
      if (storeId == null)
      {
        var product = await db.Products.FindAsync(new object[] { productId }, ct);
        if (product == null)
        {
          throw new InvalidOperationException($"Product with ID '{productId}' not found");
        }

        if (product.Stock < quantity)
        {
          throw new InvalidOperationException($"Insufficient stock. Available: {product.Stock}, Required: {quantity}");
        }

        product.Stock -= quantity;
        await db.SaveChangesAsync(ct);
        return;
      }

      // MODO STORES: Decrementar stock y liberar reserva
      var storeStock = await db.ProductStoreStock
          .FirstOrDefaultAsync(pss => pss.ProductId == productId && pss.StoreId == storeId.Value, ct);

      if (storeStock == null)
      {
        throw new InvalidOperationException($"No stock record found for product '{productId}' in store '{storeId}'");
      }

      if (storeStock.Stock < quantity)
      {
        throw new InvalidOperationException($"Insufficient stock. Available: {storeStock.Stock}, Required: {quantity}");
      }

      storeStock.Stock -= quantity;
      // También liberar la reserva si existe
      storeStock.ReservedStock = Math.Max(0, storeStock.ReservedStock - quantity);
      storeStock.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);
    }

    public async Task<List<ProductStoreStockDto>> GetProductStockByStoresAsync(Guid productId, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      var stockRecords = await db.ProductStoreStock
          .Include(pss => pss.Store)
          .Where(pss => pss.ProductId == productId)
          .OrderByDescending(pss => pss.Store!.IsDefault)
          .ThenBy(pss => pss.Store!.Name)
          .ToListAsync(ct);

      return stockRecords.Select(pss => new ProductStoreStockDto
      {
        Id = pss.Id,
        ProductId = pss.ProductId,
        StoreId = pss.StoreId,
        StoreName = pss.Store?.Name,
        Stock = pss.Stock,
        ReservedStock = pss.ReservedStock,
        AvailableStock = pss.Stock - pss.ReservedStock,
        UpdatedAt = pss.UpdatedAt
      }).ToList();
    }

    public async Task<ProductStoreStockDto> UpdateProductStoreStockAsync(Guid productId, UpdateProductStoreStockRequest request, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      // Verificar que el producto existe
      var product = await db.Products.FindAsync(new object[] { productId }, ct);
      if (product == null)
      {
        throw new InvalidOperationException($"Product with ID '{productId}' not found");
      }

      // Verificar que la tienda existe y está activa
      var store = await db.Stores.FindAsync(new object[] { request.StoreId }, ct);
      if (store == null || !store.IsActive)
      {
        throw new InvalidOperationException($"Store with ID '{request.StoreId}' not found or is inactive");
      }

      // Buscar o crear el registro de stock
      var storeStock = await db.ProductStoreStock
          .FirstOrDefaultAsync(pss => pss.ProductId == productId && pss.StoreId == request.StoreId, ct);

      if (storeStock == null)
      {
        // Crear nuevo registro
        storeStock = new ProductStoreStock
        {
          Id = Guid.NewGuid(),
          ProductId = productId,
          StoreId = request.StoreId,
          Stock = request.Stock,
          ReservedStock = 0,
          UpdatedAt = DateTime.UtcNow
        };
        db.ProductStoreStock.Add(storeStock);
      }
      else
      {
        // Actualizar existente
        storeStock.Stock = request.Stock;
        storeStock.UpdatedAt = DateTime.UtcNow;
      }

      await db.SaveChangesAsync(ct);

      // Cargar la tienda para el DTO
      await db.Entry(storeStock).Reference(pss => pss.Store).LoadAsync(ct);

      return new ProductStoreStockDto
      {
        Id = storeStock.Id,
        ProductId = storeStock.ProductId,
        StoreId = storeStock.StoreId,
        StoreName = storeStock.Store?.Name,
        Stock = storeStock.Stock,
        ReservedStock = storeStock.ReservedStock,
        AvailableStock = storeStock.Stock - storeStock.ReservedStock,
        UpdatedAt = storeStock.UpdatedAt
      };
    }

    public async Task<int> MigrateAllLegacyStockToStoreAsync(Guid targetStoreId, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      // Verificar que la tienda existe
      var store = await db.Stores.FindAsync(new object[] { targetStoreId }, ct);
      if (store == null)
      {
        throw new InvalidOperationException($"Store with ID '{targetStoreId}' not found");
      }

      // Obtener todos los productos con stock > 0
      var productsWithStock = await db.Products
          .Where(p => p.Stock > 0)
          .ToListAsync(ct);

      int migratedCount = 0;

      foreach (var product in productsWithStock)
      {
        // Verificar si ya existe un registro de stock para este producto en esta tienda
        var existingStock = await db.ProductStoreStock
            .FirstOrDefaultAsync(pss => pss.ProductId == product.Id && pss.StoreId == targetStoreId, ct);

        if (existingStock == null)
        {
          // Crear nuevo registro con el stock del producto
          var newStock = new ProductStoreStock
          {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            StoreId = targetStoreId,
            Stock = product.Stock,
            ReservedStock = 0,
            UpdatedAt = DateTime.UtcNow
          };
          db.ProductStoreStock.Add(newStock);

          // OPCIONAL: Limpiar el stock legacy (comentado por seguridad)
          // product.Stock = 0;

          migratedCount++;
        }
      }

      await db.SaveChangesAsync(ct);

      return migratedCount;
    }
  }
}
