# Sistema de Feature Flags - Guía de Uso

## Descripción General

Sistema de feature flags por tenant que permite activar/desactivar funcionalidades según el plan o personalizarlas por tenant individual. Los feature flags se almacenan en `AdminDb.Tenants.FeatureFlagsJson` y se cachean en memoria por 15 minutos.

## Características

### ? Implementado
- Feature flags por tenant en JSON
- Cache en memoria (15 minutos)
- Defaults por plan (Basic, Premium, Enterprise)
- Fallback a defaults si JSON es nulo o inválido
- Invalidación de cache al actualizar
- Endpoints SuperAdmin para gestión
- Endpoints públicos para consulta
- Uso en servicios de negocio

## Estructura de Feature Flags

### Modelo Completo
```csharp
public class TenantFeatureFlags
{
    // Checkout
    public bool AllowGuestCheckout { get; set; } = true;
    public bool RequirePhoneNumber { get; set; } = false;
    public bool EnableExpressCheckout { get; set; } = false;

    // Catalog
    public bool ShowStock { get; set; } = true;
    public bool HasVariants { get; set; } = false;
    public bool EnableWishlist { get; set; } = false;
    public bool EnableReviews { get; set; } = false;

    // Payment
    public PaymentFeatures Payments { get; set; } = new();
    
    // Cart
    public bool EnableCartSave { get; set; } = false;
    public int MaxCartItems { get; set; } = 100;

    // Search & Filters
    public bool EnableAdvancedSearch { get; set; } = false;
    public bool EnableFilters { get; set; } = true;

    // Analytics & Marketing
    public bool EnableAnalytics { get; set; } = false;
    public bool EnableNewsletterSignup { get; set; } = false;
}

public class PaymentFeatures
{
    public bool WompiEnabled { get; set; } = false;
    public bool StripeEnabled { get; set; } = false;
    public bool PayPalEnabled { get; set; } = false;
    public bool CashOnDelivery { get; set; } = true;
}
```

### Ejemplo JSON
```json
{
  "allowGuestCheckout": true,
  "requirePhoneNumber": false,
  "enableExpressCheckout": false,
  "showStock": true,
  "hasVariants": false,
  "enableWishlist": false,
  "enableReviews": false,
  "enableCartSave": false,
  "maxCartItems": 100,
  "enableAdvancedSearch": false,
  "enableFilters": true,
  "enableAnalytics": false,
  "enableNewsletterSignup": false,
  "payments": {
    "wompiEnabled": false,
    "stripeEnabled": false,
    "payPalEnabled": false,
    "cashOnDelivery": true
  }
}
```

## Defaults por Plan

### Basic
```json
{
  "allowGuestCheckout": true,
  "showStock": true,
  "hasVariants": false,
  "enableWishlist": false,
  "enableReviews": false,
  "enableAdvancedSearch": false,
  "enableAnalytics": false,
  "maxCartItems": 50,
  "payments": {
    "cashOnDelivery": true,
    "wompiEnabled": false,
    "stripeEnabled": false,
    "payPalEnabled": false
  }
}
```

### Premium
```json
{
  "allowGuestCheckout": true,
  "showStock": true,
  "hasVariants": true,
  "enableWishlist": true,
  "enableReviews": true,
  "enableAdvancedSearch": true,
  "enableAnalytics": false,
  "maxCartItems": 100,
  "payments": {
    "cashOnDelivery": true,
    "wompiEnabled": true,
    "stripeEnabled": false,
    "payPalEnabled": false
  }
}
```

### Enterprise
```json
{
  "allowGuestCheckout": true,
  "showStock": true,
  "hasVariants": true,
  "enableWishlist": true,
  "enableReviews": true,
  "enableAdvancedSearch": true,
  "enableAnalytics": true,
  "enableCartSave": true,
  "maxCartItems": 200,
  "enableExpressCheckout": true,
  "payments": {
    "cashOnDelivery": true,
    "wompiEnabled": true,
    "stripeEnabled": true,
    "payPalEnabled": true
  }
}
```

## Endpoints

### SuperAdmin Endpoints

#### GET /superadmin/tenants/{tenantId}/features
Obtiene los feature flags de un tenant.

**cURL:**
```bash
TENANT_ID="tenant-guid"

curl -X GET "http://localhost:5000/superadmin/tenants/$TENANT_ID/features"
```

**Response (200 OK):**
```json
{
  "tenantId": "guid",
  "slug": "acme",
  "plan": "Premium",
  "usingDefaults": true,
  "features": {
    "allowGuestCheckout": true,
    "showStock": true,
    "hasVariants": true,
    "enableWishlist": true,
    "maxCartItems": 100,
    "payments": {
      "wompiEnabled": true,
      "stripeEnabled": false
    }
  }
}
```

#### PATCH /superadmin/tenants/{tenantId}/features
Actualiza los feature flags de un tenant (custom).

**Body:**
```json
{
  "features": {
    "allowGuestCheckout": false,
    "showStock": true,
    "hasVariants": true,
    "maxCartItems": 150,
    "payments": {
      "wompiEnabled": true,
      "stripeEnabled": true
    }
  }
}
```

**cURL:**
```bash
TENANT_ID="tenant-guid"

curl -X PATCH "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": false,
      "showStock": true,
      "hasVariants": true,
      "maxCartItems": 150,
      "payments": {
        "wompiEnabled": true,
        "stripeEnabled": true,
        "cashOnDelivery": true
      }
    }
  }'
```

**Response (200 OK):**
```json
{
  "tenantId": "guid",
  "slug": "acme",
  "plan": "Premium",
  "usingDefaults": false,
  "features": {
    "allowGuestCheckout": false,
    "showStock": true,
    "hasVariants": true,
    "maxCartItems": 150,
    "payments": {
      "wompiEnabled": true,
      "stripeEnabled": true,
      "cashOnDelivery": true
    }
  }
}
```

**Efectos:**
- Actualiza `Tenants.FeatureFlagsJson` en Admin DB
- Invalida cache del tenant
- Próximas requests usarán los nuevos valores

#### DELETE /superadmin/tenants/{tenantId}/features
Resetea los feature flags a los defaults del plan.

**cURL:**
```bash
curl -X DELETE "http://localhost:5000/superadmin/tenants/$TENANT_ID/features"
```

**Response (200 OK):** TenantFeaturesResponse con `usingDefaults: true`

### Tenant Endpoints (requieren X-Tenant-Slug)

#### GET /api/features
Obtiene todos los feature flags del tenant actual.

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/features" \
  -H "X-Tenant-Slug: acme"
```

**Response (200 OK):**
```json
{
  "allowGuestCheckout": true,
  "showStock": true,
  "hasVariants": true,
  "enableWishlist": true,
  "maxCartItems": 100,
  "payments": {
    "wompiEnabled": true,
    "stripeEnabled": false,
    "payPalEnabled": false,
    "cashOnDelivery": true
  }
}
```

#### GET /api/features/{featureKey}
Verifica si una feature específica está habilitada.

**cURL:**
```bash
# Feature simple
curl -X GET "http://localhost:5000/api/features/allowGuestCheckout" \
  -H "X-Tenant-Slug: acme"

# Feature anidada (usar path con puntos)
curl -X GET "http://localhost:5000/api/features/payments.wompiEnabled" \
  -H "X-Tenant-Slug: acme"
```

**Response (200 OK):**
```json
{
  "featureKey": "allowGuestCheckout",
  "isEnabled": true,
  "value": true
}
```

## Uso en Código

### IFeatureService

```csharp
public interface IFeatureService
{
    // Verifica si está habilitada (booleano)
    Task<bool> IsEnabledAsync(string featureKey, CancellationToken ct = default);
    
    // Obtiene valor tipado
    Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, CancellationToken ct = default);
    
    // Obtiene todos los flags
    Task<TenantFeatureFlags> GetFeaturesAsync(CancellationToken ct = default);
    
    // Invalida cache (para SuperAdmin)
    void InvalidateCache(Guid tenantId);
}
```

### En Servicios

```csharp
public class CheckoutService : ICheckoutService
{
    private readonly IFeatureService _featureService;
    
    public async Task<PlaceOrderResponse> PlaceOrderAsync(...)
    {
        // Verificar feature booleana
        if (!userId.HasValue)
        {
            var allowGuestCheckout = await _featureService.IsEnabledAsync("AllowGuestCheckout");
            if (!allowGuestCheckout)
            {
                throw new InvalidOperationException("Guest checkout is not allowed");
            }
        }
        
        // Obtener valor numérico
        var maxCartItems = await _featureService.GetValueAsync("MaxCartItems", 100);
        if (totalItems > maxCartItems)
        {
            throw new InvalidOperationException($"Cart exceeds maximum ({maxCartItems})");
        }
        
        // Feature anidada
        var wompiEnabled = await _featureService.IsEnabledAsync("Payments.WompiEnabled");
        if (paymentMethod == "WOMPI" && !wompiEnabled)
        {
            throw new InvalidOperationException("Wompi payment is not enabled");
        }
    }
}
```

### En Endpoints

```csharp
app.MapGet("/api/products", async (IFeatureService features, ...) =>
{
    // Verificar feature
    var showStock = await features.IsEnabledAsync("ShowStock");
    
    var products = await GetProducts();
    
    // Condicional según feature
    if (!showStock)
    {
        products.ForEach(p => p.Stock = null);
    }
    
    return Results.Ok(products);
});
```

### Path Notation (Features Anidadas)

Para acceder a features anidadas, usar notación de punto:

```csharp
// JSON: { "payments": { "wompiEnabled": true } }

// Opción 1: Path completo
var enabled = await _featureService.IsEnabledAsync("Payments.WompiEnabled");

// Opción 2: GetValue con objeto
var payments = await _featureService.GetValueAsync<PaymentFeatures>("Payments");
var enabled = payments.WompiEnabled;
```

## Cache

### Configuración
- **Duración absoluta**: 15 minutos
- **Duración sliding**: 5 minutos (se renueva con cada acceso)
- **Key**: `FeatureFlags_Tenant_{TenantId}`

### Invalidación
```csharp
// Automática al actualizar via PATCH o DELETE
_featureService.InvalidateCache(tenantId);

// Manual (si es necesario)
var memoryCache = serviceProvider.GetService<IMemoryCache>();
memoryCache.Remove($"FeatureFlags_Tenant_{tenantId}");
```

### Logging
```
DEBUG: Feature flags loaded from cache for tenant {TenantId}
DEBUG: Loading feature flags from database for tenant {TenantId}
DEBUG: Using custom feature flags for tenant {TenantId}
DEBUG: Using default feature flags for plan {Plan} for tenant {TenantId}
INFO: Feature flags cache invalidated for tenant {TenantId}
INFO: Feature flags updated for tenant {TenantId} by SuperAdmin
INFO: Feature flags reset to defaults for tenant {TenantId}
```

## Flujo Completo

### 1. Tenant Nuevo (Usando Defaults)
```
Tenant creado con plan "Premium"
  ?
Primera request: GET /api/features
  ?
FeatureService.GetFeaturesAsync()
  ?
Cache miss ? LoadFeaturesFromDatabaseAsync()
  ?
FeatureFlagsJson = null ? Usar defaults de plan Premium
  ?
Guardar en cache (15 min)
  ?
Return default features
```

### 2. SuperAdmin Personaliza Features
```
PATCH /superadmin/tenants/{id}/features
  ?
Validar JSON
  ?
Actualizar Tenants.FeatureFlagsJson
  ?
InvalidateCache(tenantId)
  ?
Próximas requests usan custom features
```

### 3. Uso en Checkout
```
POST /api/checkout/place-order (guest, sin userId)
  ?
CheckoutService.PlaceOrderAsync()
  ?
featureService.IsEnabledAsync("AllowGuestCheckout")
  ?
Cache hit ? return cached features
  ?
features.IsEnabled("AllowGuestCheckout") ? true/false
  ?
Si false: throw error
Si true: continuar checkout
```

## Casos de Uso

### 1. Deshabilitar Guest Checkout
```bash
# SuperAdmin actualiza
curl -X PATCH ".../superadmin/tenants/$TENANT_ID/features" \
  -d '{"features":{"allowGuestCheckout":false}}'

# Usuario intenta checkout sin login
curl -X POST ".../api/checkout/place-order" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: guest-session"

# Response: 400 Bad Request
{
  "error": "Guest checkout is not allowed for this tenant. Please sign in."
}
```

### 2. Limitar Items del Carrito
```bash
# Basic plan: maxCartItems = 50
# Usuario intenta agregar item 51
POST /api/cart/items

# CheckoutService valida
var maxCartItems = await _featureService.GetValueAsync("MaxCartItems", 100);
if (totalItems > maxCartItems)
  throw new InvalidOperationException($"Cart exceeds maximum ({maxCartItems})");

# Response: 400 Bad Request
{
  "error": "Cart exceeds maximum allowed items (50)"
}
```

### 3. Habilitar Wompi Payment
```bash
# SuperAdmin habilita Wompi
curl -X PATCH ".../superadmin/tenants/$TENANT_ID/features" \
  -d '{"features":{"payments":{"wompiEnabled":true}}}'

# Frontend verifica disponibilidad
GET /api/features/payments.wompiEnabled

# Response:
{
  "featureKey": "payments.wompiEnabled",
  "isEnabled": true,
  "value": true
}

# Frontend muestra opción Wompi en checkout
```

## Testing

### Unit Tests
```csharp
[Test]
public async Task IsEnabledAsync_GuestCheckout_ReturnsTrue()
{
    var features = new TenantFeatureFlags { AllowGuestCheckout = true };
    var result = features.IsEnabled("AllowGuestCheckout");
    Assert.IsTrue(result);
}

[Test]
public async Task GetValueAsync_MaxCartItems_ReturnsCorrectValue()
{
    var features = new TenantFeatureFlags { MaxCartItems = 150 };
    var result = features.GetValue("MaxCartItems", 100);
    Assert.AreEqual(150, result);
}
```

### Integration Tests
```csharp
[Test]
public async Task FeatureService_LoadsFromCache_AfterFirstCall()
{
    var features1 = await _featureService.GetFeaturesAsync();
    var features2 = await _featureService.GetFeaturesAsync();
    
    // Segunda llamada debe venir del cache (misma instancia)
    Assert.AreSame(features1, features2);
}

[Test]
public async Task UpdateFeatures_InvalidatesCache()
{
    var features1 = await _featureService.GetFeaturesAsync();
    
    // Actualizar features
    await UpdateTenantFeatures(tenantId, newFeatures);
    
    var features2 = await _featureService.GetFeaturesAsync();
    
    // Debe ser diferente después de invalidar cache
    Assert.AreNotSame(features1, features2);
}
```

## Troubleshooting

### "No tenant context available"
**Causa**: IFeatureService usado sin tenant resuelto.

**Solución**: 
- Agregar header X-Tenant-Slug
- Verificar que TenantResolutionMiddleware se ejecutó

### Features no se actualizan después de PATCH
**Causa**: Cache no se invalidó.

**Solución**:
```csharp
_featureService.InvalidateCache(tenantId);
```

### JSON inválido en FeatureFlagsJson
**Causa**: JSON mal formado en base de datos.

**Solución**:
- El servicio automáticamente usa defaults si falla deserializar
- Corregir JSON en Admin DB o usar DELETE para resetear

---

**Autor**: Sistema de IA
**Fecha**: 2025-01-10
**Versión**: 1.0
