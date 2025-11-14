# ?? Documentación de la API - Actualización Completa

Se ha actualizado la documentación de la API (`README_API.md`) con los siguientes módulos nuevos:

---

## ? **Módulos Agregados**

### **1. User Authentication (Sección 3)**
- `POST /auth/register` - Registro de usuarios
- `POST /auth/login` - Autenticación con JWT
- `GET /auth/me` - Perfil del usuario autenticado

### **2. User Orders (Sección 7)**
- `GET /me/orders` - Historial de órdenes paginado
- `GET /me/orders/{orderId}` - Detalle de orden específica

### **3. User Favorites (Sección 8)**
- `GET /me/favorites` - Listar productos favoritos
- `POST /me/favorites` - Agregar a favoritos (idempotente)
- `DELETE /me/favorites/{productId}` - Eliminar de favoritos
- `GET /me/favorites/check/{productId}` - Verificar si es favorito

### **4. User Loyalty Program (Sección 9)**
- `GET /me/loyalty` - Cuenta de loyalty con balance
- `GET /me/loyalty/transactions` - Transacciones paginadas

### **5. Actualización de Checkout**
- `POST /api/checkout/place-order` ahora retorna `loyaltyPointsEarned`
- Acumulación automática de puntos para usuarios autenticados

---

## ?? **Estructura del README_API.md**

```markdown
# API Documentation - eCommerce Multi-Tenant Platform

## Secciones:

1. Tenant Provisioning
2. Public Configuration
3. ? User Authentication (NUEVO)
4. Catalog (Public)
5. Shopping Cart
6. Checkout (actualizado con loyalty)
7. ? User Orders (NUEVO)
8. ? User Favorites (NUEVO)
9. ? User Loyalty Program (NUEVO)
10. Feature Flags
11. Super Admin
12. Health & Observability
```

---

## ?? **Resumen de Endpoints Nuevos**

### **Authentication (3 endpoints)**
```
POST   /auth/register
POST   /auth/login
GET    /auth/me
```

### **Orders (2 endpoints)**
```
GET    /me/orders
GET    /me/orders/{orderId}
```

### **Favorites (4 endpoints)**
```
GET    /me/favorites
POST   /me/favorites
DELETE /me/favorites/{productId}
GET    /me/favorites/check/{productId}
```

### **Loyalty (2 endpoints)**
```
GET    /me/loyalty
GET    /me/loyalty/transactions
```

---

## ?? **Características Clave Documentadas**

### **JWT Authentication**
- ? Claims incluidos: `sub`, `email`, `tenant_id`, `tenant_slug`
- ? Expiración: 24 horas
- ? Algoritmo: HS256

### **Loyalty Points**
- ? Cálculo: `floor(orderTotal / currencyUnit) * pointsPerUnit`
- ? Configuración por tenant
- ? Acumulación automática en checkout

### **Idempotencia**
- ? Agregar favorito: No falla si ya existe
- ? Place order: Usa `Idempotency-Key`

### **Seguridad**
- ? Usuarios solo ven sus propias órdenes
- ? Usuarios solo ven sus propios favoritos
- ? Usuarios solo ven su propia cuenta de loyalty

---

## ?? **Ejemplos Completos**

### **Flujo Autenticado Completo**

```typescript
// 1. Login
const { token } = await login();

// 2. Agregar a favoritos
await addFavorite(productId, token);

// 3. Agregar al carrito
await addToCart(productId, sessionId);

// 4. Checkout (con loyalty)
const order = await placeOrder(sessionId, token);
console.log('Points earned:', order.loyaltyPointsEarned);

// 5. Ver historial
const orders = await getOrders(token);

// 6. Ver balance de loyalty
const loyalty = await getLoyalty(token);
console.log('Balance:', loyalty.balance);
```

### **Flujo Guest (Sin Auth)**

```typescript
// 1. Agregar al carrito (sin token)
await addToCart(productId, sessionId);

// 2. Guest checkout (sin token)
const order = await placeOrder(sessionId);
console.log('Points earned:', order.loyaltyPointsEarned); // null
```

---

## ?? **Interfaces TypeScript**

Todas las interfaces TypeScript están documentadas en el README:

```typescript
// Authentication
interface AuthResponse { ... }
interface UserDto { ... }
interface UserProfileDto { ... }

// Orders
interface OrderSummaryDto { ... }
interface OrderDetailDto { ... }
interface PagedOrdersResponse { ... }

// Favorites
interface FavoriteProductDto { ... }
interface FavoriteListResponse { ... }
interface AddFavoriteResponse { ... }

// Loyalty
interface LoyaltyAccountSummaryDto { ... }
interface LoyaltyTransactionDto { ... }
interface PagedLoyaltyTransactionsResponse { ... }
```

---

## ?? **Headers Requeridos**

Cada endpoint documenta claramente sus headers:

```markdown
**Headers:**
- `X-Tenant-Slug` (required)
- `Authorization: Bearer {token}` (required)
- `X-Session-Id` (conditional)
- `Content-Type: application/json`
- `Idempotency-Key` (conditional)
```

---

## ?? **Códigos de Respuesta**

Cada endpoint documenta todos los códigos de respuesta:

```markdown
#### Error Responses

- `400 Bad Request` - Validation errors
- `401 Unauthorized` - Invalid or missing token
- `404 Not Found` - Resource not found
- `409 Conflict` - Duplicate or conflict
```

---

## ?? **Cómo Usar la Documentación**

### **1. Para Desarrolladores Frontend**
- Revisa las interfaces TypeScript
- Copia los ejemplos de integración
- Usa los códigos de error para manejo

### **2. Para Testers**
- Usa los ejemplos cURL
- Verifica todos los códigos de respuesta
- Prueba casos edge (duplicados, unauthorized, etc.)

### **3. Para DevOps**
- Revisa los health checks
- Configura rate limiting
- Monitorea los endpoints críticos

---

## ? **Características Especiales**

### **Operaciones Idempotentes**
```markdown
? POST /me/favorites - No falla si producto ya existe
? POST /api/checkout/place-order - Usa Idempotency-Key
```

### **Loyalty Automático**
```markdown
? Se acumulan puntos automáticamente al completar orden
? Solo para usuarios autenticados
? Configurable por tenant
```

### **Aislamiento de Datos**
```markdown
? Usuarios solo ven SUS órdenes
? Usuarios solo ven SUS favoritos
? Usuarios solo ven SU cuenta de loyalty
```

---

## ?? **Archivo Actualizado**

El archivo `README_API.md` ha sido actualizado con:

- ? **Todas las interfaces TypeScript**
- ? **Ejemplos de request/response JSON**
- ? **Códigos de respuesta HTTP**
- ? **Headers requeridos por endpoint**
- ? **Ejemplos de integración completos**
- ? **Flujos de usuario (authenticated vs guest)**
- ? **Configuración de loyalty**
- ? **Tabla de contenidos actualizada**

---

## ?? **Changelog**

```markdown
| Version | Date | Changes |
|---------|------|---------|
| v1.2 | 2024-12-03 | Added Loyalty Program endpoints |
| v1.1 | 2024-12-02 | Added Favorites and Orders endpoints |
| v1.0 | 2024-12-01 | Initial API documentation with Authentication |
```

---

## ?? **Checklist de Documentación**

- [x] Endpoint method + route
- [x] Headers requeridos
- [x] Request body (JSON + TypeScript)
- [x] Response body (JSON + TypeScript)
- [x] Códigos de respuesta (200, 400, 401, 404, 409)
- [x] Ejemplos de request
- [x] Ejemplos de response
- [x] Ejemplos de error
- [x] Ejemplos de integración
- [x] Flujos completos de usuario

---

## ?? **¡Documentación Completa!**

La documentación de la API está:

- ? **Completamente actualizada**
- ? **Con todos los módulos nuevos**
- ? **Con ejemplos de integración**
- ? **Con interfaces TypeScript**
- ? **Con códigos de error**
- ? **Lista para usar**

---

**Archivo:** `README_API.md`  
**Versión:** 1.2  
**Última actualización:** Diciembre 2024  
**Estado:** ? Completado
