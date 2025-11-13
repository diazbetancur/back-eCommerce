# Feature Flags - Ejemplos de API

## SuperAdmin - Gestión de Feature Flags

### 1. Obtener Feature Flags de un Tenant

```bash
curl -X GET "http://localhost:5000/superadmin/tenants/123e4567-e89b-12d3-a456-426614174000/features" \
  -H "Content-Type: application/json"
```

**Respuesta exitosa (200 OK):**
```json
{
  "tenantId": "123e4567-e89b-12d3-a456-426614174000",
  "slug": "tienda-demo",
  "plan": "Premium",
  "usingDefaults": true,
  "features": {
    "allowGuestCheckout": true,
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
}
```

---

### 2. Actualizar Feature Flags de un Tenant (PATCH)

```bash
curl -X PATCH "http://localhost:5000/superadmin/tenants/123e4567-e89b-12d3-a456-426614174000/features" \
  -H "Content-Type: application/json" \
  -d '{
    "features": {
      "allowGuestCheckout": false,
      "requirePhoneNumber": true,
      "enableExpressCheckout": true,
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
      "enableCartSave": true,
      "maxCartItems": 150,
      "enableAdvancedSearch": true,
      "enableFilters": true,
      "enableAnalytics": true,
      "enableNewsletterSignup": true
    }
  }'
```

**Respuesta exitosa (200 OK):**
```json
{
  "tenantId": "123e4567-e89b-12d3-a456-426614174000",
  "slug": "tienda-demo",
  "plan": "Premium",
  "usingDefaults": false,
  "features": {
    "allowGuestCheckout": false,
    "requirePhoneNumber": true,
    "enableExpressCheckout": true,
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
    "enableCartSave": true,
    "maxCartItems": 150,
    "enableAdvancedSearch": true,
    "enableFilters": true,
    "enableAnalytics": true,
    "enableNewsletterSignup": true
  }
}
```

---

### 3. Resetear Feature Flags a Defaults del Plan

```bash
curl -X DELETE "http://localhost:5000/superadmin/tenants/123e4567-e89b-12d3-a456-426614174000/features" \
  -H "Content-Type: application/json"
```

**Respuesta exitosa (200 OK):**
```json
{
  "tenantId": "123e4567-e89b-12d3-a456-426614174000",
  "slug": "tienda-demo",
  "plan": "Premium",
  "usingDefaults": true,
  "features": {
    "allowGuestCheckout": true,
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
}
```

---

## Tenant - Consulta de Feature Flags

### 4. Obtener Feature Flags del Tenant Actual

```bash
curl -X GET "http://localhost:5000/api/features" \
  -H "X-Tenant-Slug: tienda-demo" \
  -H "Content-Type: application/json"
```

**Respuesta exitosa (200 OK):**
```json
{
  "allowGuestCheckout": true,
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
```

---

### 5. Verificar una Feature Específica

```bash
curl -X GET "http://localhost:5000/api/features/payments.wompiEnabled" \
  -H "X-Tenant-Slug: tienda-demo" \
  -H "Content-Type: application/json"
```

**Respuesta exitosa (200 OK):**
```json
{
  "featureKey": "payments.wompiEnabled",
  "isEnabled": true,
  "value": true
}
```

---

## Checkout - Validación de Features

### 6. Intento de Checkout sin JWT cuando allowGuestCheckout = false

```bash
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: tienda-demo" \
  -H "X-Session-Id: sess_abc123" \
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
  }'
```

**Respuesta cuando allowGuestCheckout = false y no hay JWT:**
```json
HTTP/1.1 401 Unauthorized
```

---

### 7. Intento de Pago con Wompi cuando está Deshabilitado

```bash
curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: tienda-demo" \
  -H "X-Session-Id: sess_abc123" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIs..." \
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
    "paymentMethod": "wompi"
  }'
```

**Respuesta cuando payments.wompiEnabled = false:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "El método de pago Wompi no está disponible para este tenant"
}
```

---

## Escenarios de Uso por Plan

### Plan Basic
```json
{
  "allowGuestCheckout": true,
  "showStock": true,
  "hasVariants": false,
  "enableWishlist": false,
  "enableReviews": false,
  "payments": {
    "wompiEnabled": false,
    "cashOnDelivery": true
  },
  "maxCartItems": 50,
  "enableAdvancedSearch": false,
  "enableAnalytics": false
}
```

### Plan Premium
```json
{
  "allowGuestCheckout": true,
  "showStock": true,
  "hasVariants": true,
  "enableWishlist": true,
  "enableReviews": true,
  "payments": {
    "wompiEnabled": true,
    "cashOnDelivery": true
  },
  "maxCartItems": 100,
  "enableAdvancedSearch": true,
  "enableAnalytics": false
}
```

### Plan Enterprise
```json
{
  "allowGuestCheckout": true,
  "showStock": true,
  "hasVariants": true,
  "enableWishlist": true,
  "enableReviews": true,
  "enableExpressCheckout": true,
  "payments": {
    "wompiEnabled": true,
    "stripeEnabled": true,
    "payPalEnabled": true,
    "cashOnDelivery": true
  },
  "enableCartSave": true,
  "maxCartItems": 200,
  "enableAdvancedSearch": true,
  "enableAnalytics": true
}
```

---

## Notas Importantes

1. **Cache**: Los feature flags se cachean por 15 minutos. Después de actualizar vía SuperAdmin, el cache se invalida automáticamente.

2. **Defaults vs Custom**: 
   - `usingDefaults: true` = usando los defaults del plan
   - `usingDefaults: false` = usando configuración personalizada

3. **Validación en Checkout**:
   - Si `allowGuestCheckout = false` ? requiere JWT válido
   - Si método de pago no está habilitado ? retorna 400 Bad Request

4. **Paths Anidados**: Para features anidadas usar notación de punto:
   - `payments.wompiEnabled`
   - `payments.stripeEnabled`
   - etc.

5. **Headers Requeridos**:
   - Tenant endpoints: `X-Tenant-Slug`
   - Checkout: `X-Tenant-Slug` + `X-Session-Id`
   - Autenticado: + `Authorization: Bearer <token>`
