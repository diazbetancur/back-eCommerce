# 🏗️ Repository Pattern & Unit of Work - Guía de Implementación

## Resumen

Se ha implementado un patrón de **Repository + Unit of Work** estandarizado para operaciones multi-tenant.

## Archivos Creados

| Archivo | Ubicación | Propósito |
|---------|-----------|-----------|
| `ITenantRepository<T>` | CC.Domain/Interfaces/Repositories/ | Interfaz genérica de repositorio |
| `ISpecification<T>` | CC.Domain/Interfaces/Repositories/ | Interfaz para Specification Pattern |
| `BaseSpecification<T>` | CC.Domain/Specifications/ | Implementación base de especificaciones |
| `TenantRepository<T>` | CC.Infraestructure/Tenant/Repositories/ | Implementación del repositorio |
| `ITenantUnitOfWork` | CC.Infraestructure/Tenant/ | Interfaz del Unit of Work |
| `TenantUnitOfWork` | CC.Infraestructure/Tenant/ | Implementación del UoW |
| `ITenantUnitOfWorkFactory` | CC.Infraestructure/Tenant/ | Factory interface |
| `TenantUnitOfWorkFactory` | CC.Infraestructure/Tenant/ | Factory implementation |
| `QueryableExtensions` | CC.Infraestructure/Tenant/Extensions/ | Extensiones para queries |
| `CartServiceV2` | CC.Aplication/Services/ | Ejemplo refactorizado |

## Cómo Usar

### 1. Inyectar el Factory

```csharp
public class MiServicio : IMiServicio
{
    private readonly ITenantUnitOfWorkFactory _uowFactory;
    
    public MiServicio(ITenantUnitOfWorkFactory uowFactory)
    {
        _uowFactory = uowFactory;
    }
}
```

### 2. Crear y Usar el UoW

```csharp
public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct)
{
    await using var uow = _uowFactory.Create();
    
    // Usar repositorios tipados
    var product = new Product { Name = request.Name, Price = request.Price };
    uow.Products.Add(product);
    
    await uow.SaveChangesAsync(ct);
    
    return new ProductDto(product);
}
```

### 3. Usar Transacciones

```csharp
public async Task PlaceOrderAsync(OrderRequest request, CancellationToken ct)
{
    await using var uow = _uowFactory.Create();
    
    await uow.BeginTransactionAsync(ct);
    try
    {
        // Crear orden
        var order = new Order { ... };
        uow.Orders.Add(order);
        
        // Crear items
        foreach (var item in request.Items)
        {
            uow.OrderItems.Add(new OrderItem { OrderId = order.Id, ... });
        }
        
        // Actualizar stock
        foreach (var item in request.Items)
        {
            var product = await uow.Products.GetByIdAsync(item.ProductId, ct);
            product.Stock -= item.Quantity;
            uow.Products.Update(product);
        }
        
        await uow.CommitAsync(ct); // Guarda y commit
    }
    catch
    {
        await uow.RollbackAsync(ct);
        throw;
    }
}
```

### 4. Queries Avanzadas

```csharp
// Paginación
var (items, total) = await uow.Products.GetPagedAsync(
    page: 1,
    pageSize: 20,
    predicate: p => p.IsActive,
    orderBy: q => q.OrderByDescending(p => p.CreatedAt),
    ct
);

// Usando Query directamente con extensiones
var result = await uow.Products.QueryNoTracking
    .WhereIf(categoryId.HasValue, p => p.CategoryId == categoryId)
    .WhereIfNotEmpty(search, p => p.Name.Contains(search))
    .ToPagedResultAsync(page, pageSize, ct);
```

### 5. Repositorio Dinámico

```csharp
// Para entidades no predefinidas en el UoW
var repo = uow.Repository<MiEntidadCustom>();
var items = await repo.GetAllAsync(ct);
```

## Repositorios Disponibles

El `ITenantUnitOfWork` expone repositorios para todas las entidades:

```csharp
// Authentication & Authorization
uow.Users
uow.Roles
uow.UserRoles
uow.Modules
uow.RoleModulePermissions

// User Accounts (Consumer)
uow.UserAccounts
uow.UserProfiles

// Catalog
uow.Products
uow.Categories
uow.ProductCategories
uow.ProductImages

// Shopping Cart
uow.Carts
uow.CartItems

// Orders
uow.Orders
uow.OrderItems
uow.OrderStatuses

// Favorites
uow.FavoriteProducts

// Loyalty Program
uow.LoyaltyAccounts
uow.LoyaltyTransactions

// Settings
uow.Settings
uow.WebPushSubscriptions
```

## Migración de Servicios Existentes

### Antes (usando TenantDbContextFactory directamente):

```csharp
public class MyService
{
    private readonly TenantDbContextFactory _dbFactory;
    
    public async Task DoSomething(CancellationToken ct)
    {
        await using var db = _dbFactory.Create();
        
        var product = await db.Products.FindAsync(id);
        product.Name = "Updated";
        await db.SaveChangesAsync(ct);
    }
}
```

### Después (usando ITenantUnitOfWorkFactory):

```csharp
public class MyService
{
    private readonly ITenantUnitOfWorkFactory _uowFactory;
    
    public async Task DoSomething(CancellationToken ct)
    {
        await using var uow = _uowFactory.Create();
        
        var product = await uow.Products.GetByIdAsync(id, ct);
        product.Name = "Updated";
        uow.Products.Update(product);
        await uow.SaveChangesAsync(ct);
    }
}
```

## Beneficios

1. **Consistencia**: Un solo patrón para todos los servicios
2. **Transacciones**: Manejo centralizado de transacciones
3. **Testabilidad**: Fácil de mockear `ITenantUnitOfWork`
4. **Type Safety**: Repositorios tipados evitan errores
5. **Extensibilidad**: Fácil agregar nuevas entidades
6. **Separación de Concerns**: La lógica de acceso a datos está encapsulada

## Siguiente Paso: Deprecar Servicios Legacy

Para completar la migración:

1. Cambiar registro en `Program.cs`:
   ```csharp
   // Cambiar de:
   builder.Services.AddScoped<ICartService, CartService>();
   // A:
   builder.Services.AddScoped<ICartService, CartServiceV2>();
   ```

2. Una vez verificado que funciona, eliminar `CartService` original.

3. Repetir para otros servicios que usen `TenantDbContextFactory` directamente.

## Registro DI

Ya está registrado en `Program.cs`:

```csharp
builder.Services.AddScoped<ITenantUnitOfWorkFactory, TenantUnitOfWorkFactory>();
```
