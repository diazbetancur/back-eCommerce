# Feature Flag EnableMultiStore - Ejemplos de Uso

## Descripción

Se implementó el feature flag `EnableMultiStore` para controlar el acceso a funcionalidades multi-tienda según el plan del tenant.

## Configuración por Plan

### Plan BASIC ($5/mes)
- ✅ `EnableMultiStore`: `false` (solo 1 tienda)
- ✅ Límite de tiendas: **1**

### Plan PREMIUM ($15/mes)
- ✅ `EnableMultiStore`: `true` (múltiples tiendas)
- ✅ Límite de tiendas: **20**

### Plan ENTERPRISE
- ✅ `EnableMultiStore`: `true` (múltiples tiendas)
- ✅ Límite de tiendas: **200** (o ilimitado según configuración)

---

## Cómo Consultar el Feature Flag

### Endpoint: GET /api/tenant-config

Este endpoint retorna todos los feature flags del tenant autenticado, incluyendo el nuevo `multistore`.

### Request

```http
GET https://your-api.com/api/tenant-config
Authorization: Bearer {JWT_TOKEN}
```

### Respuesta Exitosa (200 OK)

#### Tenant con Plan BASIC (sin multi-tienda)

```json
{
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantSlug": "tienda-basic",
  "tenantName": "tienda-basic",
  "features": {
    "loyalty": false,
    "multistore": false,
    "paymentsWompiEnabled": false,
    "allowGuestCheckout": true,
    "showStock": true,
    "enableReviews": false,
    "enableAdvancedSearch": false,
    "enableAnalytics": false
  }
}
```

**Nota**: `"multistore": false` indica que este tenant NO puede usar múltiples tiendas.

---

#### Tenant con Plan PREMIUM (con multi-tienda)

```json
{
  "tenantId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tenantSlug": "tienda-premium",
  "tenantName": "tienda-premium",
  "features": {
    "loyalty": true,
    "multistore": true,
    "paymentsWompiEnabled": true,
    "allowGuestCheckout": true,
    "showStock": true,
    "enableReviews": true,
    "enableAdvancedSearch": true,
    "enableAnalytics": false
  }
}
```

**Nota**: `"multistore": true` indica que este tenant PUEDE usar hasta 20 tiendas.

---

## Consultar Límite de Tiendas

Para conocer el límite exacto de tiendas permitidas por plan, necesitas consultar los `PlanLimits` en la base de datos:

### SQL Query (Admin DB)

```sql
-- Ver límites de tiendas por plan
SELECT 
    p."Code" as plan_code,
    p."Name" as plan_name,
    pl."LimitValue" as max_stores,
    pl."Description"
FROM admin."Plans" p
JOIN admin."PlanLimits" pl ON pl."PlanId" = p."Id"
WHERE pl."LimitCode" = 'max_stores'
ORDER BY p."Code";
```

### Resultado Esperado

| plan_code | plan_name              | max_stores | Description                     |
|-----------|------------------------|------------|---------------------------------|
| Basic     | Plan Básico - $5/mes   | 1          | Solo 1 tienda (plan básico)     |
| Premium   | Plan Premium - $15/mes | 20         | Máximo 20 tiendas               |

---

## Uso en Frontend (React/TypeScript)

### 1. Obtener Configuración del Tenant

```typescript
interface TenantConfig {
  tenantId: string;
  tenantSlug: string;
  tenantName: string;
  features: {
    loyalty: boolean;
    multistore: boolean;
    paymentsWompiEnabled: boolean;
    allowGuestCheckout: boolean;
    showStock: boolean;
    enableReviews: boolean;
    enableAdvancedSearch: boolean;
    enableAnalytics: boolean;
  };
}

const getTenantConfig = async (): Promise<TenantConfig> => {
  const response = await fetch('/api/tenant-config', {
    headers: {
      'Authorization': `Bearer ${getAccessToken()}`
    }
  });
  
  if (!response.ok) {
    throw new Error('Error al obtener configuración del tenant');
  }
  
  return response.json();
};
```

### 2. Mostrar/Ocultar Funcionalidades Multi-Tienda

```typescript
const App = () => {
  const [config, setConfig] = useState<TenantConfig | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadConfig();
  }, []);

  const loadConfig = async () => {
    try {
      const data = await getTenantConfig();
      setConfig(data);
    } catch (error) {
      console.error('Error:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div>Cargando...</div>;

  return (
    <div>
      <h1>Admin Dashboard</h1>
      
      {/* Menú de navegación */}
      <nav>
        <ul>
          <li><a href="/products">Productos</a></li>
          <li><a href="/orders">Órdenes</a></li>
          
          {/* Solo mostrar si multi-tienda está habilitado */}
          {config?.features.multistore && (
            <li><a href="/stores">Gestión de Tiendas</a></li>
          )}
          
          {config?.features.loyalty && (
            <li><a href="/loyalty">Programa de Lealtad</a></li>
          )}
        </ul>
      </nav>
    </div>
  );
};
```

### 3. Validar Límite al Crear Tiendas

```typescript
const StoreCreationForm = () => {
  const [config, setConfig] = useState<TenantConfig | null>(null);
  const [currentStoresCount, setCurrentStoresCount] = useState(0);
  const [maxStores, setMaxStores] = useState(1); // Default Basic

  useEffect(() => {
    loadConfig();
    loadCurrentStores();
    loadPlanLimits();
  }, []);

  const loadConfig = async () => {
    const data = await getTenantConfig();
    setConfig(data);
  };

  const loadCurrentStores = async () => {
    // GET /api/admin/stores
    const response = await fetch('/api/admin/stores', {
      headers: { 'Authorization': `Bearer ${getAccessToken()}` }
    });
    const stores = await response.json();
    setCurrentStoresCount(stores.length);
  };

  const loadPlanLimits = async () => {
    // Obtener límites del plan (endpoint a implementar)
    // Por ahora asumir: Basic = 1, Premium = 20
    setMaxStores(config?.features.multistore ? 20 : 1);
  };

  const handleCreateStore = async (storeData: any) => {
    // Validar límite
    if (currentStoresCount >= maxStores) {
      alert(`Has alcanzado el límite de ${maxStores} tienda(s) de tu plan`);
      return;
    }

    // Si no tiene multi-tienda habilitado
    if (!config?.features.multistore) {
      alert('Tu plan no incluye soporte para múltiples tiendas. Actualiza a Premium.');
      return;
    }

    // Crear tienda
    const response = await fetch('/api/admin/stores', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${getAccessToken()}`
      },
      body: JSON.stringify(storeData)
    });

    if (response.ok) {
      alert('Tienda creada exitosamente');
      loadCurrentStores(); // Recargar contador
    }
  };

  return (
    <div>
      <h2>Crear Nueva Tienda</h2>
      
      {!config?.features.multistore && (
        <div className="alert alert-warning">
          ⚠️ Tu plan solo permite 1 tienda. 
          <a href="/upgrade">Actualiza a Premium</a> para crear hasta 20 tiendas.
        </div>
      )}

      <div className="info-box">
        📊 Tiendas actuales: {currentStoresCount} / {maxStores}
      </div>

      {currentStoresCount < maxStores ? (
        <form onSubmit={handleCreateStore}>
          {/* Formulario de creación */}
        </form>
      ) : (
        <div className="alert alert-danger">
          ❌ Has alcanzado el límite de tiendas de tu plan.
        </div>
      )}
    </div>
  );
};
```

---

## Validación en Backend

### Implementar Servicio de Validación de Límites

```csharp
public interface IPlanLimitService
{
    Task<bool> CanCreateStoreAsync(CancellationToken ct = default);
    Task<int> GetMaxStoresAsync(CancellationToken ct = default);
    Task<int> GetCurrentStoresCountAsync(CancellationToken ct = default);
}

public class PlanLimitService : IPlanLimitService
{
    private readonly TenantDbContext _tenantDb;
    private readonly AdminDbContext _adminDb;
    private readonly ITenantAccessor _tenantAccessor;

    public async Task<bool> CanCreateStoreAsync(CancellationToken ct = default)
    {
        var maxStores = await GetMaxStoresAsync(ct);
        var currentCount = await GetCurrentStoresCountAsync(ct);
        
        return currentCount < maxStores;
    }

    public async Task<int> GetMaxStoresAsync(CancellationToken ct = default)
    {
        var tenantInfo = _tenantAccessor.TenantInfo;
        
        // Buscar tenant en Admin DB
        var tenant = await _adminDb.Tenants
            .Include(t => t.Plan)
            .ThenInclude(p => p.Limits)
            .FirstOrDefaultAsync(t => t.Id == tenantInfo.Id, ct);
        
        if (tenant == null) return 1; // Default
        
        var storeLimit = tenant.Plan.Limits
            .FirstOrDefault(l => l.LimitCode == PlanLimitCodes.MaxStores);
        
        return storeLimit?.LimitValue ?? 1;
    }

    public async Task<int> GetCurrentStoresCountAsync(CancellationToken ct = default)
    {
        return await _tenantDb.Stores
            .Where(s => s.IsActive)
            .CountAsync(ct);
    }
}
```

### Validar en Endpoint de Creación de Tienda

```csharp
app.MapPost("/api/admin/stores", async (
    CreateStoreRequest request,
    IStoreService storeService,
    IPlanLimitService planLimitService) =>
{
    // Validar límite de tiendas
    var canCreate = await planLimitService.CanCreateStoreAsync();
    
    if (!canCreate)
    {
        var max = await planLimitService.GetMaxStoresAsync();
        var current = await planLimitService.GetCurrentStoresCountAsync();
        
        return Results.BadRequest(new 
        { 
            error = $"Store limit exceeded",
            message = $"Your plan allows {max} store(s). You currently have {current} store(s).",
            currentStores = current,
            maxStores = max
        });
    }
    
    // Crear tienda
    var store = await storeService.CreateStoreAsync(request);
    return Results.Created($"/api/admin/stores/{store.Id}", store);
})
.RequireAuthorization()
.RequireModule("inventory", "create");
```

---

## Respuestas de Error

### 400 Bad Request - Límite Excedido

```json
{
  "error": "Store limit exceeded",
  "message": "Your plan allows 1 store(s). You currently have 1 store(s).",
  "currentStores": 1,
  "maxStores": 1
}
```

### 403 Forbidden - Feature No Habilitado

```json
{
  "error": "Feature not available",
  "message": "Multi-store feature is not enabled for your plan. Upgrade to Premium to unlock this feature."
}
```

---

## Testing

### Test: Verificar Feature Flag por Plan

```csharp
[Fact]
public void DefaultFeatureFlags_BasicPlan_ShouldHaveMultiStoreFalse()
{
    // Arrange & Act
    var features = DefaultFeatureFlags.GetForPlan("BASIC");
    
    // Assert
    Assert.False(features.EnableMultiStore);
}

[Fact]
public void DefaultFeatureFlags_PremiumPlan_ShouldHaveMultiStoreTrue()
{
    // Arrange & Act
    var features = DefaultFeatureFlags.GetForPlan("PREMIUM");
    
    // Assert
    Assert.True(features.EnableMultiStore);
}
```

### Test: Endpoint Retorna Multistore Correcto

```csharp
[Fact]
public async Task GetTenantConfig_WithPremiumPlan_ShouldReturnMultistoreTrue()
{
    // Arrange
    var client = _factory.CreateAuthenticatedClient("premium-tenant");
    
    // Act
    var response = await client.GetAsync("/api/tenant-config");
    var config = await response.Content.ReadFromJsonAsync<TenantConfigResponse>();
    
    // Assert
    Assert.NotNull(config);
    Assert.True(config.Features.Multistore);
}
```

---

## Archivos Modificados

1. ✅ [CC.Domain/Tenancy/TenantFeatureFlags.cs](CC.Domain/Tenancy/TenantFeatureFlags.cs#L18)
   - Agregada propiedad `EnableMultiStore`
   - Configurada por plan en `DefaultFeatureFlags.GetForPlan()`

2. ✅ [CC.Domain/Features/FeatureKeys.cs](CC.Domain/Features/FeatureKeys.cs#L17)
   - Agregada constante `EnableMultiStore = "enableMultiStore"`

3. ✅ [CC.Infraestructure/Admin/Entities/PlanLimit.cs](CC.Infraestructure/Admin/Entities/PlanLimit.cs#L49)
   - Agregada constante `MaxStores = "max_stores"`

4. ✅ [CC.Infraestructure/Admin/PlanLimitsSeeder.cs](CC.Infraestructure/Admin/PlanLimitsSeeder.cs)
   - Basic: `max_stores = 1`
   - Premium: `max_stores = 20`

5. ✅ [Api-eCommerce/Controllers/TenantConfigController.cs](Api-eCommerce/Controllers/TenantConfigController.cs#L92)
   - Mapeo: `Multistore = features.EnableMultiStore`

---

## Próximos Pasos

1. ✅ Crear migración para aplicar seeds de PlanLimits en BD existentes
2. ⏳ Implementar `IPlanLimitService` con validaciones en tiempo real
3. ⏳ Agregar endpoint GET /api/admin/plan-limits para consultar límites del tenant
4. ⏳ Validar límite en CreateStore endpoint
5. ⏳ Agregar tests de integración para validación de límites

---

**Versión**: 1.0  
**Fecha**: 20 de enero de 2026  
**Autor**: Backend Team
