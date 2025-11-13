# ?? QUICK START: Feature Flags

## ? Inicio Rápido en 5 Minutos

### 1?? Verificar Instalación

```bash
# Verificar que el servidor está corriendo
curl http://localhost:5000/health
```

### 2?? Crear un Tenant de Prueba

```bash
# Crear tenant Premium
curl -X POST "http://localhost:5000/superadmin/tenants/provision" \
  -H "Content-Type: application/json" \
  -d '{
    "slug": "mi-tienda",
    "plan": "Premium",
    "adminEmail": "admin@mitienda.com",
    "adminPassword": "MiPassword123!@#"
  }'

# Copiar el tenantId de la respuesta
```

### 3?? Ver Features Actuales

```bash
# Reemplazar {TENANT_ID} con el ID del paso anterior
curl -X GET "http://localhost:5000/superadmin/tenants/{TENANT_ID}/features"
```

**Verás algo como:**
```json
{
  "tenantId": "...",
  "slug": "mi-tienda",
  "plan": "Premium",
  "usingDefaults": true,
  "features": {
    "allowGuestCheckout": true,
    "payments": {
      "wompiEnabled": true,
      "cashOnDelivery": true
    }
  }
}
```

### 4?? Deshabilitar Guest Checkout

```bash
curl -X PATCH "http://localhost:5000/superadmin/tenants/{TENANT_ID}/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": false,
      "requirePhoneNumber": false,
      "enableExpressCheckout": false,
      "showStock": true,
      "hasVariants": true,
      "enableWishlist": true,
      "enableReviews": true,
      "payments": {
        "wompiEnabled": true,
        "stripeEnabled": false,
        "payPalEnabled": false,
        "cashOnDelivery": true
      },
      "enableCartSave": false,
      "maxCartItems": 100,
      "enableAdvancedSearch": true,
      "enableFilters": true,
      "enableAnalytics": false,
      "enableNewsletterSignup": false
    }
  }'
```

### 5?? Probar Validación en Checkout

```bash
# Intentar checkout SIN JWT (debe fallar con 401)
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: mi-tienda" \
  -H "X-Session-Id: test-session-123" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Juan Pérez",
      "phone": "+573001234567",
      "address": "Calle 123 #45-67",
      "city": "Bogotá",
      "country": "CO",
      "postalCode": "110111"
    },
    "paymentMethod": "cash"
  }' \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado esperado:** `HTTP Status: 401` ?

---

## ?? Casos de Uso Comunes

### Caso 1: Habilitar Stripe para un Tenant

```bash
curl -X PATCH "http://localhost:5000/superadmin/tenants/{TENANT_ID}/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": true,
      "payments": {
        "wompiEnabled": true,
        "stripeEnabled": true,
        "payPalEnabled": false,
        "cashOnDelivery": true
      }
    }
  }'
```

### Caso 2: Aumentar Límite de Items en Carrito

```bash
curl -X PATCH "http://localhost:5000/superadmin/tenants/{TENANT_ID}/features" \
  -d '{
    "features": {
      "maxCartItems": 200,
      ...
    }
  }'
```

### Caso 3: Habilitar Todas las Features (Enterprise)

```bash
curl -X PATCH "http://localhost:5000/superadmin/tenants/{TENANT_ID}/features" \
  -d '{
    "features": {
      "allowGuestCheckout": true,
      "requirePhoneNumber": true,
      "enableExpressCheckout": true,
      "showStock": true,
      "hasVariants": true,
      "enableWishlist": true,
      "enableReviews": true,
      "payments": {
        "wompiEnabled": true,
        "stripeEnabled": true,
        "payPalEnabled": true,
        "cashOnDelivery": true
      },
      "enableCartSave": true,
      "maxCartItems": 200,
      "enableAdvancedSearch": true,
      "enableFilters": true,
      "enableAnalytics": true,
      "enableNewsletterSignup": true
    }
  }'
```

### Caso 4: Resetear a Defaults del Plan

```bash
# Volver a la configuración estándar del plan
curl -X DELETE "http://localhost:5000/superadmin/tenants/{TENANT_ID}/features"
```

---

## ?? Troubleshooting

### Problema: "Tenant not found"
**Solución:** Verifica que el TENANT_ID sea correcto.

### Problema: 401 Unauthorized en checkout
**Posibles causas:**
1. ? **Esperado:** `allowGuestCheckout = false` y no hay JWT
2. ? **Error:** Falta header `X-Tenant-Slug`

### Problema: 400 "Wompi no disponible"
**Posibles causas:**
1. ? **Esperado:** `payments.wompiEnabled = false` en el tenant
2. ? **Error:** Verificar que el feature flag esté correcto

### Problema: Cache no se invalida
**Solución:**
- El cache se invalida automáticamente al hacer PATCH
- Espera 2 segundos después de actualizar
- Verifica los logs: `Feature flags cache invalidated`

---

## ?? Feature Keys Disponibles

```csharp
// Checkout
"allowGuestCheckout"
"requirePhoneNumber"
"enableExpressCheckout"

// Payments
"payments.wompiEnabled"
"payments.stripeEnabled"
"payments.payPalEnabled"
"payments.cashOnDelivery"

// Catalog
"showStock"
"hasVariants"
"enableWishlist"
"enableReviews"

// Cart
"enableCartSave"
"maxCartItems"

// Search
"enableAdvancedSearch"
"enableFilters"

// Analytics
"enableAnalytics"
"enableNewsletterSignup"
```

---

## ?? Ejemplos de Integración en Código

### Verificar si una Feature está Habilitada

```csharp
// En un endpoint o servicio
public async Task<IResult> MyEndpoint(
    [FromServices] IFeatureService featureService)
{
    // Verificar si Wompi está habilitado
    var wompiEnabled = await featureService.IsEnabledAsync(
        FeatureKeys.PaymentsWompiEnabled);
    
    if (!wompiEnabled)
    {
        return Results.Problem(
            "Wompi no está disponible", 
            statusCode: 400);
    }
    
    // Continuar con la lógica...
}
```

### Obtener Valor de una Feature

```csharp
// Obtener límite de items en carrito
var maxItems = await featureService.GetValueAsync<int>(
    FeatureKeys.MaxCartItems, 
    defaultValue: 100);

if (cartItemCount > maxItems)
{
    return Results.Problem($"Máximo {maxItems} items permitidos");
}
```

### Obtener Todas las Features

```csharp
// Obtener todas las features del tenant actual
var features = await featureService.GetFeaturesAsync();

return Results.Ok(new {
    allowGuestCheckout = features.AllowGuestCheckout,
    payments = features.Payments,
    maxCartItems = features.MaxCartItems
});
```

---

## ?? Más Documentación

- **Ejemplos Completos:** `FEATURE-FLAGS-API-EXAMPLES.md`
- **Testing:** `FEATURE-FLAGS-TESTING-GUIDE.md`
- **Arquitectura:** `FEATURE-FLAGS-COMPLETE-IMPLEMENTATION.md`
- **Resumen Final:** `FEATURE-FLAGS-FINAL-DELIVERY.md`

---

## ? Checklist de Verificación

- [ ] Servidor corriendo en http://localhost:5000
- [ ] Tenant creado con éxito
- [ ] Features se obtienen correctamente
- [ ] Features se actualizan correctamente
- [ ] Validación en checkout funciona (401 sin JWT)
- [ ] Validación de métodos de pago funciona (400 sin Wompi)
- [ ] Cache se invalida al actualizar

---

## ?? ¡Listo!

Ya tienes el sistema de feature flags funcionando. Ahora puedes:

1. ? Configurar features por tenant
2. ? Validar permisos en checkout
3. ? Controlar métodos de pago disponibles
4. ? Personalizar experiencia por plan

**¿Necesitas ayuda?** Consulta los archivos de documentación completos.
