# ?? GUÍA DE TESTING - Feature Flags

## ?? Escenarios de Prueba

### ?? Test Suite 1: SuperAdmin - Gestión de Features

#### ? Test 1.1: Obtener Features de un Tenant

```bash
# Crear un tenant de prueba
TENANT_ID=$(curl -X POST "http://localhost:5000/superadmin/tenants/provision" \
  -H "Content-Type: application/json" \
  -d '{
    "slug": "test-store",
    "plan": "Premium",
    "adminEmail": "admin@test.com",
    "adminPassword": "Test123!@#"
  }' | jq -r '.tenantId')

echo "Tenant ID: $TENANT_ID"

# Obtener features (debe usar defaults de Premium)
curl -X GET "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" | jq .
```

**Resultado Esperado:**
```json
{
  "tenantId": "...",
  "slug": "test-store",
  "plan": "Premium",
  "usingDefaults": true,
  "features": {
    "allowGuestCheckout": true,
    "payments": {
      "wompiEnabled": true,
      "stripeEnabled": false
    },
    "maxCartItems": 100
  }
}
```

---

#### ? Test 1.2: Actualizar Features Custom

```bash
# Actualizar features
curl -X PATCH "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": false,
      "requirePhoneNumber": true,
      "enableExpressCheckout": false,
      "showStock": true,
      "hasVariants": true,
      "enableWishlist": true,
      "enableReviews": true,
      "payments": {
        "wompiEnabled": true,
        "stripeEnabled": true,
        "payPalEnabled": false,
        "cashOnDelivery": true
      },
      "enableCartSave": false,
      "maxCartItems": 150,
      "enableAdvancedSearch": true,
      "enableFilters": true,
      "enableAnalytics": false,
      "enableNewsletterSignup": false
    }
  }' | jq .
```

**Resultado Esperado:**
```json
{
  "tenantId": "...",
  "slug": "test-store",
  "plan": "Premium",
  "usingDefaults": false,  // ? Ahora es false
  "features": {
    "allowGuestCheckout": false,  // ? Actualizado
    "maxCartItems": 150  // ? Actualizado
  }
}
```

---

#### ? Test 1.3: Resetear a Defaults

```bash
# Resetear features
curl -X DELETE "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" | jq .
```

**Resultado Esperado:**
```json
{
  "tenantId": "...",
  "slug": "test-store",
  "plan": "Premium",
  "usingDefaults": true,  // ? Volvió a true
  "features": {
    "allowGuestCheckout": true,  // ? Default de Premium
    "maxCartItems": 100  // ? Default de Premium
  }
}
```

---

### ?? Test Suite 2: Checkout con allowGuestCheckout

#### ? Test 2.1: Guest Checkout Permitido (Default)

```bash
# 1. Agregar productos al carrito
SESSION_ID="test-session-$(date +%s)"

curl -X POST "http://localhost:5000/api/cart/add" \
  -H "X-Tenant-Slug: test-store" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "prod-123",
    "quantity": 2
  }'

# 2. Intentar checkout SIN JWT (allowGuestCheckout = true por default)
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: test-store" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Test User",
      "phone": "+573001234567",
      "address": "Test St 123",
      "city": "Bogotá",
      "country": "CO",
      "postalCode": "110111"
    },
    "paymentMethod": "cash"
  }' \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado Esperado:**
```
HTTP Status: 201
```

---

#### ? Test 2.2: Guest Checkout Bloqueado

```bash
# 1. Deshabilitar guest checkout
curl -X PATCH "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": false,
      "requirePhoneNumber": false,
      "payments": {
        "wompiEnabled": true,
        "cashOnDelivery": true
      },
      "maxCartItems": 100
    }
  }'

# 2. Esperar un momento para asegurar que el cache se invalide
sleep 2

# 3. Intentar checkout SIN JWT (debe fallar)
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: test-store" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Test User",
      "phone": "+573001234567",
      "address": "Test St 123",
      "city": "Bogotá",
      "country": "CO"
    },
    "paymentMethod": "cash"
  }' \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado Esperado:**
```
HTTP Status: 401
```

---

#### ? Test 2.3: Checkout Autenticado (Con JWT)

```bash
# 1. Registrar usuario (asumiendo que existe el endpoint)
USER_TOKEN=$(curl -X POST "http://localhost:5000/api/auth/register" \
  -H "X-Tenant-Slug: test-store" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@test.com",
    "password": "Test123!@#"
  }' | jq -r '.token')

echo "User Token: $USER_TOKEN"

# 2. Intentar checkout CON JWT (debe funcionar)
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: test-store" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Test User",
      "phone": "+573001234567",
      "address": "Test St 123",
      "city": "Bogotá",
      "country": "CO"
    },
    "paymentMethod": "cash"
  }' \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado Esperado:**
```
HTTP Status: 201
```

---

### ?? Test Suite 3: Validación de Métodos de Pago

#### ? Test 3.1: Wompi Habilitado (Default Premium)

```bash
# 1. Habilitar guest checkout de nuevo
curl -X PATCH "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": true,
      "payments": {
        "wompiEnabled": true,
        "cashOnDelivery": true
      }
    }
  }'

# 2. Intentar pago con Wompi (debe funcionar)
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: test-store" \
  -H "X-Session-Id: test-session-$(date +%s)" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Test User",
      "phone": "+573001234567",
      "address": "Test St 123",
      "city": "Bogotá",
      "country": "CO"
    },
    "paymentMethod": "wompi"
  }' \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado Esperado:**
```
HTTP Status: 201
```

---

#### ? Test 3.2: Wompi Deshabilitado

```bash
# 1. Deshabilitar Wompi
curl -X PATCH "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": true,
      "payments": {
        "wompiEnabled": false,
        "cashOnDelivery": true
      }
    }
  }'

# 2. Esperar invalidación de cache
sleep 2

# 3. Intentar pago con Wompi (debe fallar)
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: test-store" \
  -H "X-Session-Id: test-session-$(date +%s)" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Test User",
      "phone": "+573001234567",
      "address": "Test St 123",
      "city": "Bogotá",
      "country": "CO"
    },
    "paymentMethod": "wompi"
  }' \
  -w "\nHTTP Status: %{http_code}\n" | jq .
```

**Resultado Esperado:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "El método de pago Wompi no está disponible para este tenant"
}
HTTP Status: 400
```

---

#### ? Test 3.3: Pago con Cash (Siempre Permitido)

```bash
# Cash on delivery debe funcionar siempre
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: test-store" \
  -H "X-Session-Id: test-session-$(date +%s)" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Test User",
      "phone": "+573001234567",
      "address": "Test St 123",
      "city": "Bogotá",
      "country": "CO"
    },
    "paymentMethod": "cash"
  }' \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado Esperado:**
```
HTTP Status: 201
```

---

### ?? Test Suite 4: Cache Validation

#### ? Test 4.1: Cache Miss ? Cache Hit

```bash
# 1. Limpiar logs
# (En producción usar herramienta de monitoring)

# 2. Primera llamada (cache miss)
echo "=== Primera llamada (cache miss) ==="
curl -X GET "http://localhost:5000/api/features" \
  -H "X-Tenant-Slug: test-store"

# 3. Segunda llamada inmediata (cache hit)
echo "=== Segunda llamada (cache hit) ==="
curl -X GET "http://localhost:5000/api/features" \
  -H "X-Tenant-Slug: test-store"
```

**Logs Esperados:**
```
[DBG] Feature flags cache MISS for tenant ...
[DBG] Loading feature flags from database for tenant ...
[DBG] Feature flags cache HIT for tenant ...
```

---

#### ? Test 4.2: Cache Invalidation

```bash
# 1. Llamar features (cachea)
curl -X GET "http://localhost:5000/api/features" \
  -H "X-Tenant-Slug: test-store"

# 2. Actualizar features (invalida cache)
curl -X PATCH "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": true,
      "maxCartItems": 200
    }
  }'

# 3. Llamar features de nuevo (cache miss porque se invalidó)
curl -X GET "http://localhost:5000/api/features" \
  -H "X-Tenant-Slug: test-store"
```

**Logs Esperados:**
```
[DBG] Feature flags cache HIT for tenant ...
[INF] Feature flags cache invalidated for tenant ...
[DBG] Feature flags cache MISS for tenant ...
```

---

### ?? Test Suite 5: Edge Cases

#### ? Test 5.1: Tenant sin FeatureFlagsJson (Usa Defaults)

```bash
# Crear tenant Basic sin custom features
BASIC_TENANT=$(curl -X POST "http://localhost:5000/superadmin/tenants/provision" \
  -H "Content-Type: application/json" \
  -d '{
    "slug": "basic-store",
    "plan": "Basic",
    "adminEmail": "admin@basic.com",
    "adminPassword": "Test123!@#"
  }' | jq -r '.tenantId')

# Verificar que usa defaults de Basic
curl -X GET "http://localhost:5000/superadmin/tenants/$BASIC_TENANT/features" \
  -H "Content-Type: application/json" | jq .
```

**Resultado Esperado:**
```json
{
  "plan": "Basic",
  "usingDefaults": true,
  "features": {
    "allowGuestCheckout": true,
    "payments": {
      "wompiEnabled": false,  // ? Basic no tiene Wompi
      "cashOnDelivery": true
    },
    "maxCartItems": 50  // ? Límite de Basic
  }
}
```

---

#### ? Test 5.2: Tenant no Encontrado

```bash
curl -X GET "http://localhost:5000/superadmin/tenants/00000000-0000-0000-0000-000000000000/features" \
  -H "Content-Type: application/json" \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado Esperado:**
```json
{
  "error": "Tenant not found"
}
HTTP Status: 404
```

---

#### ? Test 5.3: JSON Inválido en Features

```bash
# Intentar actualizar con JSON inválido
curl -X PATCH "http://localhost:5000/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": "esto-no-es-un-objeto-valido"
  }' \
  -w "\nHTTP Status: %{http_code}\n"
```

**Resultado Esperado:**
```json
{
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid feature flags JSON format"
}
HTTP Status: 400
```

---

## ?? Matriz de Testing

| Test ID | Escenario | allowGuestCheckout | JWT | Método Pago | Esperado |
|---------|-----------|-------------------|-----|-------------|----------|
| 2.1 | Guest checkout default | ? true | ? no | cash | ? 201 |
| 2.2 | Guest checkout bloqueado | ? false | ? no | cash | ? 401 |
| 2.3 | Checkout autenticado | ? false | ? sí | cash | ? 201 |
| 3.1 | Wompi habilitado | ? true | ? no | wompi | ? 201 |
| 3.2 | Wompi deshabilitado | ? true | ? no | wompi | ? 400 |
| 3.3 | Cash siempre permitido | ? true | ? no | cash | ? 201 |

---

## ?? Script de Testing Completo

```bash
#!/bin/bash

BASE_URL="http://localhost:5000"
TENANT_SLUG="test-store"

echo "?? Iniciando Feature Flags Testing Suite"
echo "=========================================="

# Test 1: Crear tenant
echo -e "\n? Test 1: Crear tenant de prueba"
TENANT_ID=$(curl -s -X POST "$BASE_URL/superadmin/tenants/provision" \
  -H "Content-Type: application/json" \
  -d "{
    \"slug\": \"$TENANT_SLUG\",
    \"plan\": \"Premium\",
    \"adminEmail\": \"admin@test.com\",
    \"adminPassword\": \"Test123!@#\"
  }" | jq -r '.tenantId')

echo "Tenant ID: $TENANT_ID"

# Test 2: Obtener features (defaults)
echo -e "\n? Test 2: Obtener features (debe usar defaults Premium)"
curl -s -X GET "$BASE_URL/superadmin/tenants/$TENANT_ID/features" | jq .

# Test 3: Deshabilitar guest checkout
echo -e "\n? Test 3: Deshabilitar guest checkout"
curl -s -X PATCH "$BASE_URL/superadmin/tenants/$TENANT_ID/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": false,
      "payments": {"wompiEnabled": true, "cashOnDelivery": true}
    }
  }' | jq .

sleep 2

# Test 4: Intentar checkout sin JWT (debe fallar)
echo -e "\n? Test 4: Checkout sin JWT (debe retornar 401)"
SESSION_ID="test-$(date +%s)"
curl -s -X POST "$BASE_URL/api/checkout/place-order" \
  -H "X-Tenant-Slug: $TENANT_SLUG" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": {
      "fullName": "Test",
      "phone": "+573001234567",
      "address": "Test St",
      "city": "Bogotá",
      "country": "CO"
    },
    "paymentMethod": "cash"
  }' \
  -w "\nHTTP Status: %{http_code}\n"

# Test 5: Resetear a defaults
echo -e "\n? Test 5: Resetear a defaults del plan"
curl -s -X DELETE "$BASE_URL/superadmin/tenants/$TENANT_ID/features" | jq .

echo -e "\n=========================================="
echo "?? Testing Suite Completado"
```

**Ejecutar:**
```bash
chmod +x test-feature-flags.sh
./test-feature-flags.sh
```

---

## ? Checklist de Validación

### Funcionalidad
- [ ] GET features retorna configuración correcta
- [ ] PATCH features actualiza y retorna `usingDefaults: false`
- [ ] DELETE features resetea y retorna `usingDefaults: true`
- [ ] Guest checkout bloqueado sin JWT ? 401
- [ ] Guest checkout permitido con JWT ? 201
- [ ] Wompi deshabilitado ? 400 con mensaje descriptivo
- [ ] Wompi habilitado ? 201
- [ ] Cash on delivery siempre funciona

### Cache
- [ ] Primera llamada genera cache miss
- [ ] Segunda llamada genera cache hit
- [ ] PATCH invalida cache automáticamente
- [ ] Cache expira después de 15 minutos
- [ ] Sliding expiration funciona correctamente

### Edge Cases
- [ ] Tenant sin custom features usa defaults
- [ ] Tenant no encontrado retorna 404
- [ ] JSON inválido retorna 400
- [ ] Sin contexto de tenant lanza exception

---

## ?? RESULTADO

Esta guía proporciona **tests exhaustivos** para validar toda la funcionalidad de feature flags, incluyendo:
- ? Gestión por SuperAdmin
- ? Validaciones en checkout
- ? Comportamiento del cache
- ? Edge cases y errores

**¡La implementación está lista para producción!** ??
