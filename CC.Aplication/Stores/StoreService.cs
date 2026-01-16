using CC.Domain.Dto;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace CC.Aplication.Stores
{
  public interface IStoreService
  {
    Task<List<StoreDto>> GetAllStoresAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<StoreDto?> GetStoreByIdAsync(Guid id, CancellationToken ct = default);
    Task<StoreDto?> GetDefaultStoreAsync(CancellationToken ct = default);
    Task<StoreDto> CreateStoreAsync(CreateStoreRequest request, CancellationToken ct = default);
    Task<StoreDto> UpdateStoreAsync(Guid id, UpdateStoreRequest request, CancellationToken ct = default);
    Task DeleteStoreAsync(Guid id, CancellationToken ct = default);
    Task<StoreDto> SetDefaultStoreAsync(Guid id, CancellationToken ct = default);
  }

  public class StoreService : IStoreService
  {
    private readonly TenantDbContextFactory _dbFactory;

    public StoreService(TenantDbContextFactory dbFactory)
    {
      _dbFactory = dbFactory;
    }

    public async Task<List<StoreDto>> GetAllStoresAsync(bool includeInactive = false, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      var query = db.Stores.AsQueryable();

      if (!includeInactive)
      {
        query = query.Where(s => s.IsActive);
      }

      var stores = await query
          .OrderByDescending(s => s.IsDefault)
          .ThenBy(s => s.Name)
          .ToListAsync(ct);

      return stores.Select(MapToDto).ToList();
    }

    public async Task<StoreDto?> GetStoreByIdAsync(Guid id, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      var store = await db.Stores.FindAsync(new object[] { id }, ct);

      return store != null ? MapToDto(store) : null;
    }

    public async Task<StoreDto?> GetDefaultStoreAsync(CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      var store = await db.Stores
          .FirstOrDefaultAsync(s => s.IsDefault && s.IsActive, ct);

      return store != null ? MapToDto(store) : null;
    }

    public async Task<StoreDto> CreateStoreAsync(CreateStoreRequest request, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      // Validar código único si se proporciona
      if (!string.IsNullOrWhiteSpace(request.Code))
      {
        var existingStore = await db.Stores
            .FirstOrDefaultAsync(s => s.Code == request.Code, ct);

        if (existingStore != null)
        {
          throw new InvalidOperationException($"Store with code '{request.Code}' already exists");
        }
      }

      // Si es la primera tienda o se marca como default, establecerla como default
      var isFirstStore = !await db.Stores.AnyAsync(ct);
      var shouldBeDefault = request.IsDefault || isFirstStore;

      // Si se marca como default, quitar el default de otras tiendas
      if (shouldBeDefault)
      {
        var currentDefault = await db.Stores
            .Where(s => s.IsDefault)
            .ToListAsync(ct);

        foreach (var store in currentDefault)
        {
          store.IsDefault = false;
        }
      }

      var newStore = new Store
      {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Code = request.Code,
        Address = request.Address,
        City = request.City,
        Country = request.Country,
        Phone = request.Phone,
        IsDefault = shouldBeDefault,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
      };

      db.Stores.Add(newStore);
      await db.SaveChangesAsync(ct);

      return MapToDto(newStore);
    }

    public async Task<StoreDto> UpdateStoreAsync(Guid id, UpdateStoreRequest request, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      var store = await db.Stores.FindAsync(new object[] { id }, ct);
      if (store == null)
      {
        throw new InvalidOperationException($"Store with ID '{id}' not found");
      }

      // Validar código único si se cambia
      if (!string.IsNullOrWhiteSpace(request.Code) && request.Code != store.Code)
      {
        var existingStore = await db.Stores
            .FirstOrDefaultAsync(s => s.Code == request.Code && s.Id != id, ct);

        if (existingStore != null)
        {
          throw new InvalidOperationException($"Store with code '{request.Code}' already exists");
        }
      }

      // Si se marca como default, quitar el default de otras tiendas
      if (request.IsDefault && !store.IsDefault)
      {
        var currentDefault = await db.Stores
            .Where(s => s.IsDefault && s.Id != id)
            .ToListAsync(ct);

        foreach (var s in currentDefault)
        {
          s.IsDefault = false;
        }
      }

      // No permitir desactivar la tienda default
      if (!request.IsActive && store.IsDefault)
      {
        throw new InvalidOperationException("Cannot deactivate the default store. Set another store as default first.");
      }

      store.Name = request.Name;
      store.Code = request.Code;
      store.Address = request.Address;
      store.City = request.City;
      store.Country = request.Country;
      store.Phone = request.Phone;
      store.IsDefault = request.IsDefault;
      store.IsActive = request.IsActive;
      store.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);

      return MapToDto(store);
    }

    public async Task DeleteStoreAsync(Guid id, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      var store = await db.Stores.FindAsync(new object[] { id }, ct);
      if (store == null)
      {
        throw new InvalidOperationException($"Store with ID '{id}' not found");
      }

      // No permitir eliminar la tienda default
      if (store.IsDefault)
      {
        throw new InvalidOperationException("Cannot delete the default store. Set another store as default first.");
      }

      // Verificar si hay stock asociado a esta tienda
      var hasStock = await db.ProductStoreStock
          .AnyAsync(pss => pss.StoreId == id, ct);

      if (hasStock)
      {
        throw new InvalidOperationException("Cannot delete store with associated stock. Transfer or remove stock first.");
      }

      // Verificar si hay órdenes asociadas
      var hasOrders = await db.Orders
          .AnyAsync(o => o.StoreId == id, ct);

      if (hasOrders)
      {
        throw new InvalidOperationException("Cannot delete store with associated orders. Consider deactivating instead.");
      }

      db.Stores.Remove(store);
      await db.SaveChangesAsync(ct);
    }

    public async Task<StoreDto> SetDefaultStoreAsync(Guid id, CancellationToken ct = default)
    {
      await using var db = _dbFactory.Create();

      var store = await db.Stores.FindAsync(new object[] { id }, ct);
      if (store == null)
      {
        throw new InvalidOperationException($"Store with ID '{id}' not found");
      }

      if (!store.IsActive)
      {
        throw new InvalidOperationException("Cannot set an inactive store as default");
      }

      // Quitar el default de todas las otras tiendas
      var allStores = await db.Stores.ToListAsync(ct);
      foreach (var s in allStores)
      {
        s.IsDefault = (s.Id == id);
      }

      await db.SaveChangesAsync(ct);

      return MapToDto(store);
    }

    private static StoreDto MapToDto(Store store)
    {
      return new StoreDto
      {
        Id = store.Id,
        Name = store.Name,
        Code = store.Code,
        Address = store.Address,
        City = store.City,
        Country = store.Country,
        Phone = store.Phone,
        IsDefault = store.IsDefault,
        IsActive = store.IsActive,
        CreatedAt = store.CreatedAt,
        UpdatedAt = store.UpdatedAt
      };
    }
  }
}
