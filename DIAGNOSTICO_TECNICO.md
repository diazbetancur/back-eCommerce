# 📋 DIAGNÓSTICO TÉCNICO - eCommerce Multitenant
**Fecha**: 20 de enero de 2026  
**Tech Lead**: Backend Review  
**Objetivo**: Retomar contexto y definir plan de acción sin romper lo existente

---

## 1️⃣ DIAGNÓSTICO DE ESTADO ACTUAL

### 🔧 Resolución de Tenant

#### **Mecanismo de Detección**
Multi-fuente con prioridad (Header > Query > Host):
```
1. Header: X-Tenant-Slug (prioritario)
2. Query parameter: ?tenant=xxx
3. Host/subdomain: primer segmento del hostname
```

#### **Flujo de Resolución**
```
HTTP Request
    ↓
[UseAuthentication] → Valida JWT
    ↓
[TenantResolutionMiddleware] → Extrae slug de header/query/host
    ↓
[TenantResolver] → Busca tenant en AdminDb
    ↓
[TenantAccessor] → Almacena TenantInfo en scope del request
    ↓
[TenantDbContextFactory] → Crea DbContext con ConnectionString del tenant
    ↓
Controller/Service → Opera sobre TenantDb
```

**Archivos clave**:
- `Api-eCommerce/Middleware/TenantResolutionMiddleware.cs` - Middleware global
- `CC.Infraestructure/Tenancy/TenantResolver.cs` - Lógica de resolución
- `CC.Infraestructure/Tenancy/TenantAccessor.cs` - Almacenamiento scoped
- `CC.Infraestructure/Tenant/TenantDbContextFactory.cs` - Factory de contexto

#### **Rutas Excluidas** (no requieren tenant)
```
/swagger
/health
/admin         → SuperAdmin endpoints (usa AdminDb)
/provision     → Tenant provisioning
/superadmin    → SuperAdmin management
/_framework    → Blazor routes
/_vs           → Visual Studio tooling
```

#### **Validaciones Implementadas**
- ✅ Tenant existe en AdminDb
- ✅ Status = `Ready` (rechaza: Pending, Seeding, Suspended, Failed, Disabled)
- ✅ ConnectionString desencriptada con DataProtection

#### **Estados de Tenant**
```csharp
public enum TenantStatus
{
    Pending = 0,    // Creado, esperando aprovisionamiento
    Seeding = 1,    // Ejecutando migraciones y seed
    Ready = 2,      // ✅ Operativo
    Suspended = 3,  // Suspendido por admin
    Failed = 4,     // Error en aprovisionamiento
    Disabled = 5    // Deshabilitado
}
```

---

### 🗄️ Estrategia de Aislamiento de Datos

#### **Tipo: Database-per-Tenant** (Aislamiento físico completo)

```
┌─────────────────────────────────────────────────────┐
│  AdminDb (ecommerce_admin)                          │
│  Schema: admin                                      │
│                                                     │
│  Tablas:                                            │
│  - Tenants (metadata, plan, status, encrypted CS)  │
│  - Plans & Features                                │
│  - PlanFeatures & PlanLimits                       │
│  - TenantFeatureOverrides                          │
│  - TenantUsageDaily (metering)                     │
│  - TenantProvisioning (background jobs)            │
│  - AdminUsers & AdminRoles (super-admin)           │
└─────────────────────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┬───────────────┐
        ▼               ▼               ▼               ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ ecom_tenant  │  │ ecom_tenant  │  │ ecom_tenant  │  │ ecom_tenant  │
│ _tienda1     │  │ _tienda2     │  │ _tienda3     │  │ _tiendaN     │
│              │  │              │  │              │  │              │
│ Schema: pub  │  │ Schema: pub  │  │ Schema: pub  │  │ Schema: pub  │
│              │  │              │  │              │  │              │
│ - Products   │  │ - Products   │  │ - Products   │  │ - Products   │
│ - Categories │  │ - Categories │  │ - Categories │  │ - Categories │
│ - Orders     │  │ - Orders     │  │ - Orders     │  │ - Orders     │
│ - Cart       │  │ - Cart       │  │ - Cart       │  │ - Cart       │
│ - Users      │  │ - Users      │  │ - Users      │  │ - Users      │
│ - Roles      │  │ - Roles      │  │ - Roles      │  │ - Roles      │
│ - Stores     │  │ - Stores     │  │ - Stores     │  │ - Stores     │
│ - Stock      │  │ - Stock      │  │ - Stock      │  │ - Stock      │
│ - Loyalty    │  │ - Loyalty    │  │ - Loyalty    │  │ - Loyalty    │
└──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘
```

#### **Infraestructura**
- **Host**: Aiven Cloud PostgreSQL
- **Puerto**: 19544
- **Template**: `Tenancy:TenantDbTemplate` con placeholder `{DbName}`
- **Seguridad**: SSL Mode=Require, ConnectionStrings encriptadas con DataProtection

#### **Ventajas de esta arquitectura**
- ✅ **Zero chance de data leak** entre tenants
- ✅ Backup/restore **independiente** por tenant
- ✅ Performance **predecible** (no row-level filtering)
- ✅ Migración de schema **independiente** por tenant
- ✅ Escalado **granular** (mover tenants a otros servidores)
- ✅ Cumplimiento **GDPR** simplificado (borrar DB = borrar tenant)

#### **Desventajas a considerar**
- ⚠️ Costos de DB más altos (vs shared DB)
- ⚠️ Overhead de conexiones (pool por tenant)
- ⚠️ Migraciones deben ejecutarse en múltiples DBs

---

### 🔐 Autenticación y Autorización

#### **JWT Authentication**
```csharp
// Configuración actual (Program.cs)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(x => x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,          // ⚠️ RIESGO MENOR
        ValidateAudience = false,        // ⚠️ RIESGO MENOR
        ValidateLifetime = true,         // ✅ OK
        ValidateIssuerSigningKey = true, // ✅ OK
        IssuerSigningKey = new SymmetricSecurityKey(...),
        ClockSkew = TimeSpan.Zero
    });
```

**Claims estándar**:
- `sub` o `ClaimTypes.NameIdentifier` → UserId
- Roles, Módulos (si aplica)

**⚠️ Problema detectado**:
- JWT **NO incluye tenantId** actualmente
- Tenant se resuelve del header, **NO del token**
- **Riesgo**: Usuario de tenant1 podría intentar acceder a tenant2 si no se valida ownership

#### **Sistema de Permisos (RBAC + Módulos)**

```
User (tenant-scoped)
  ↓ N:N
UserRole
  ↓
Role (Admin, Seller, Viewer, etc.)
  ↓ N:N
RoleModulePermission
  ↓
Module (catalog, orders, inventory, loyalty)
  ↓
Permissions: [view, create, update, delete]
```

**Entidades** (en TenantDb):
- `Users` - Usuarios finales del eCommerce (tenant-scoped)
- `Roles` - Roles personalizables por tenant
- `Modules` - Módulos del sistema (catalog, orders, etc.)
- `RoleModulePermissions` - Matriz de permisos

**Validación de permisos**:
```csharp
[RequireModule("catalog", "view")]        // Atributo
[ServiceFilter(typeof(ModuleAuthorizationActionFilter))] // Filter
```

**Ejemplo de uso**:
```csharp
[HttpGet]
[Authorize]
[RequireModule("orders", "view")]
public async Task<IActionResult> GetOrders() { ... }
```

#### **Separación Admin vs Tenant Users**

| Aspecto | AdminUsers (AdminDb) | Users (TenantDb) |
|---------|---------------------|------------------|
| **Propósito** | Gestión de tenants, planes, super-admin | Usuarios finales del eCommerce |
| **Autenticación** | JWT separado (admin endpoints) | JWT tenant-scoped |
| **Base de datos** | ecommerce_admin | ecom_tenant_xxx |
| **Endpoints** | `/admin/`, `/superadmin/` | `/me/`, `/api/` |

---

## 2️⃣ PENDIENTES PRIORIZADOS

### 🔴 P0 - CRÍTICO (Bloquean go-live)

| # | Pendiente | Impacto | Esfuerzo | Riesgo | Archivos Afectados | Dependencias |
|---|-----------|---------|----------|--------|--------------------|--------------|
| **1** | **Validación tenant-user ownership** | 🔴 Crítico | 1-2 días | Alto | Todos los servicios con `userId` del JWT | Auth middleware |
| | **Descripción**: Validar que `user.TenantId == tenant actual` en CADA operación. Sin esto, un usuario de tenant1 podría acceder a datos de tenant2. | | | | | |
| | **Solución**: Crear interceptor/middleware que valide ownership automáticamente | | | | | |
| **2** | **Integración de Pagos (Wompi)** | 🔴 Alto | 5-8 días | Alto | `PaymentService.cs`, `PaymentController.cs`, `Wompi/` | CheckoutService, OrderService |
| | **Descripción**: Wompi está mencionado en código pero no implementado. Sin esto, órdenes quedan en PENDING indefinidamente. | | | | | |
| | **Incluye**: SDK Wompi, CreatePaymentIntent, VerifyPayment, webhook handler | | | | | |
| **3** | **Webhook de pagos** | 🔴 Alto | 2-3 días | Alto | `/webhooks/payments/wompi` | PaymentService |
| | **Descripción**: Endpoint para recibir notificaciones de cambio de estado de pago (HMAC validation) | | | | | |
| | **Seguridad**: Validación de firma, IP whitelisting opcional | | | | | |

**Total P0**: ~8-13 días de desarrollo

---

### 🟡 P1 - IMPORTANTE (Necesario para producción robusta)

| # | Pendiente | Impacto | Esfuerzo | Riesgo | Archivos Afectados | Dependencias |
|---|-----------|---------|----------|--------|--------------------|--------------|
| **4** | **Rate limiting por tenant** | 🟡 Medio | 2-3 días | Medio | Middleware, `TenantUsageTracking` | AdminDb.TenantUsageDaily |
| | **Descripción**: Prevenir abuso (DDoS, scraping). Límites basados en plan (Basic: 100 req/min, Premium: 500, etc.) | | | | | |
| **5** | **Inventory reservation** | 🟡 Medio | 3-4 días | Medio | `CheckoutService`, tabla `StockReservation` | OrderService |
| | **Descripción**: Al crear orden, reservar stock por 15 min. Si pago falla, liberar. Evita race conditions. | | | | | |
| | **Flujo**: CreateOrder → Reserve → PaymentSuccess → Confirm → PaymentFail → Release | | | | | |
| **6** | **Saga pattern (Order orchestration)** | 🟡 Medio | 5-7 días | Alto | `OrderOrchestrator`, `CompensationHandlers` | Payment, Stock, Email |
| | **Descripción**: Transacciones distribuidas para garantizar consistencia (Order + Payment + Stock + Email) | | | | | |
| **7** | **Cálculo dinámico de envío** | 🟡 Medio | 2-3 días | Bajo | `ShippingService.cs`, tablas `ShippingZone`, `ShippingRate` | CheckoutService |
| | **Descripción**: Actualmente hardcoded. Necesita: zonas geográficas, peso, tarifas por carrier | | | | | |
| **8** | **Cálculo de impuestos por región** | 🟡 Medio | 3-4 días | Medio | `TaxService.cs`, tabla `TaxRule` | CheckoutService |
| | **Descripción**: Tasa fija actual (15%). Necesita: por ciudad/departamento/país, productos exentos | | | | | |
| **9** | **Email notifications** | 🟡 Medio | 2-3 días | Bajo | `EmailService.cs`, templates, background worker | OrderService |
| | **Descripción**: Orden creada, pagada, enviada, cancelada. Templates HTML personalizables por tenant | | | | | |
| **10** | **Auditoría completa** | 🟠 Bajo | 3-4 días | Bajo | `AuditableEntity`, interceptor, tabla `AuditLog` | DbContext.SaveChanges |
| | **Descripción**: CreatedBy, UpdatedBy, CreatedAt, UpdatedAt en todas las entidades. Log de cambios críticos. | | | | | |

**Total P1**: ~22-31 días de desarrollo

---

### 🟢 P2 - MEJORAS (Nice to have)

| # | Pendiente | Impacto | Esfuerzo | Archivos Afectados | Descripción |
|---|-----------|---------|----------|-----------------------|-------------|
| **11** | **Validación de Plan Limits** | 🟠 Bajo | 2 días | `PlanLimitService` (ya existe) | Validar en runtime: max_products, max_orders_month, max_storage_mb |
| **12** | **Caché distribuido (Redis)** | 🟠 Bajo | 3-4 días | `DistributedCacheService.cs` | MemoryCache actual no escala multi-instancia |
| **13** | **Correlation ID** | 🟠 Bajo | 1 día | Middleware, ILogger | X-Correlation-ID para trazabilidad E2E |
| **14** | **Metering/Facturación** | 🟠 Bajo | 4-5 días | `MeteringService`, `BillingService` | Facturación por uso (orders, API calls, storage) |
| **15** | **Full-text search** | 🟢 Muy Bajo | 3-4 días | ElasticSearch o PG full-text | Búsqueda avanzada en productos |
| **16** | **Order admin endpoints** | 🟡 Medio | 2 días | `OrdersAdminController` | GET /admin/orders, PUT status, POST ship |
| **17** | **Reports/Analytics** | 🟢 Bajo | 5-7 días | `ReportsController` | Sales, top products, revenue by period |
| **18** | **Notifications push (PWA)** | 🟢 Bajo | 3-4 días | WebPushService (parcial existe) | Push notifications para órdenes |

**Total P2**: ~23-32 días de desarrollo

---

## 3️⃣ CONTRATOS API

### 📦 Catálogo (Público - requiere tenant)

#### **Productos Públicos**
```http
GET /api/products?page=1&pageSize=20&categoryId={guid}&search=camisa
Headers:
  X-Tenant-Slug: tienda1
  # o query param: ?tenant=tienda1

Response 200:
{
  "items": [
    {
      "id": "guid",
      "name": "Camisa Azul Premium",
      "slug": "camisa-azul-premium",
      "price": 89000,
      "compareAtPrice": 120000,
      "stock": 50,
      "mainImageUrl": "https://...",
      "isFeatured": true,
      "tags": "verano,oferta,nuevo"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 145,
  "totalPages": 8
}
```

#### **Detalle de Producto**
```http
GET /api/products/{id}
GET /api/products/slug/{slug}

Response 200:
{
  "id": "guid",
  "name": "Camisa Azul Premium",
  "description": "Descripción completa...",
  "price": 89000,
  "stock": 50,
  "categories": [
    { "id": "guid", "name": "Ropa Hombre", "slug": "ropa-hombre" }
  ],
  "images": [
    { "url": "https://...", "isPrimary": true },
    { "url": "https://...", "isPrimary": false }
  ]
}
```

#### **Categorías**
```http
GET /api/categories?includeInactive=false

Response 200:
[
  {
    "id": "guid",
    "name": "Ropa Hombre",
    "slug": "ropa-hombre",
    "imageUrl": "https://...",
    "parentId": null,
    "children": [
      { "id": "guid", "name": "Camisas", "slug": "camisas" }
    ]
  }
]
```

---

### 🛒 Carrito de Compras

**Headers requeridos**:
```http
X-Tenant-Slug: tienda1
X-Session-Id: {guid generado en frontend}
```

#### **Obtener Carrito**
```http
GET /api/cart

Response 200:
{
  "id": "guid",
  "sessionId": "abc123",
  "items": [
    {
      "id": "guid",
      "productId": "guid",
      "productName": "Camisa Azul",
      "price": 89000,
      "quantity": 2,
      "subtotal": 178000
    }
  ],
  "itemCount": 2,
  "subtotal": 178000,
  "createdAt": "2026-01-20T10:00:00Z",
  "updatedAt": "2026-01-20T10:15:00Z"
}
```

#### **Agregar al Carrito**
```http
POST /api/cart/items
Content-Type: application/json

{
  "productId": "guid",
  "quantity": 2
}

Response 200: { ... cart completo ... }
Response 400: { "error": "Insufficient stock" }
```

#### **Actualizar Cantidad**
```http
PUT /api/cart/items/{itemId}

{
  "quantity": 5
}

Response 200: { ... cart completo ... }
Response 404: { "error": "Cart item not found" }
```

#### **Eliminar Item / Vaciar Carrito**
```http
DELETE /api/cart/items/{itemId}  → 204 No Content
DELETE /api/cart                 → 204 No Content
```

---

### 💳 Checkout

**Headers requeridos**:
```http
X-Tenant-Slug: tienda1
X-Session-Id: {session-guid}
Idempotency-Key: {guid}  # ⚠️ OBLIGATORIO en place-order
Authorization: Bearer {jwt}  # Opcional según FeatureFlags.AllowGuestCheckout
```

#### **Obtener Quote (Cotización)**
```http
POST /api/checkout/quote
Content-Type: application/json

{
  "shippingAddress": "Calle 123 #45-67, Bogotá",
  "shippingMethod": "standard"  # opcional
}

Response 200:
{
  "subtotal": 178000,
  "tax": 26700,          # 15%
  "shipping": 12000,
  "total": 216700,
  "items": [
    {
      "productId": "guid",
      "productName": "Camisa Azul",
      "price": 89000,
      "quantity": 2,
      "subtotal": 178000
    }
  ]
}

Response 400:
{
  "error": "Cart is empty"
}
```

#### **Crear Orden (Place Order)**
```http
POST /api/checkout/place-order
Headers:
  X-Tenant-Slug: tienda1
  X-Session-Id: abc123
  Idempotency-Key: unique-key-123  # ⚠️ OBLIGATORIO
  Authorization: Bearer {jwt}      # si AllowGuestCheckout=false

Body:
{
  "shippingAddress": "Calle 123 #45-67, Bogotá, Colombia",
  "email": "user@example.com",
  "phone": "+573001234567",
  "paymentMethod": "wompi",
  "storeId": "guid"  # opcional, para multi-location pickup
}

Response 201 Created:
{
  "orderId": "guid",
  "orderNumber": "ORD-20260120-0001",
  "total": 216700,
  "subtotal": 178000,
  "tax": 26700,
  "shipping": 12000,
  "status": "PENDING",
  "createdAt": "2026-01-20T10:30:00Z"
}

Response 400:
{
  "error": "Insufficient stock for Camisa Azul. Available: 1"
}

Response 409 Conflict:
{
  "error": "Order already created with this idempotency key"
}
```

**⚠️ Importante**:
- Si `FeatureFlags.AllowGuestCheckout = false` → requiere JWT (401 sin auth)
- `Idempotency-Key` previene duplicados (si se reintenta el request)
- Si `paymentMethod = "wompi"` → validar que `FeatureFlags.PaymentsWompiEnabled = true`

---

### 📦 Órdenes (Usuario Autenticado)

#### **Mis Órdenes**
```http
GET /me/orders?page=1&pageSize=20&status=PENDING&fromDate=2026-01-01&toDate=2026-01-31
Headers:
  X-Tenant-Slug: tienda1
  Authorization: Bearer {jwt}

Response 200:
{
  "items": [
    {
      "id": "guid",
      "orderNumber": "ORD-20260120-0001",
      "total": 216700,
      "status": "PENDING",
      "createdAt": "2026-01-20T10:30:00Z",
      "itemCount": 2
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 15,
  "totalPages": 1
}

Response 401: Unauthorized (JWT inválido/faltante)
Response 403: Forbidden (sin permiso orders:view)
```

#### **Detalle de Orden**
```http
GET /me/orders/{orderId}

Response 200:
{
  "id": "guid",
  "orderNumber": "ORD-20260120-0001",
  "total": 216700,
  "subtotal": 178000,
  "tax": 26700,
  "shipping": 12000,
  "status": "PENDING",
  "shippingAddress": "Calle 123 #45-67, Bogotá",
  "email": "user@example.com",
  "phone": "+573001234567",
  "paymentMethod": "wompi",
  "items": [
    {
      "productId": "guid",
      "productName": "Camisa Azul",
      "price": 89000,
      "quantity": 2,
      "subtotal": 178000
    }
  ],
  "createdAt": "2026-01-20T10:30:00Z",
  "completedAt": null
}

Response 404: Order not found o no pertenece al usuario
```

---

### ⭐ Favoritos

```http
GET /me/favorites
Headers:
  X-Tenant-Slug: tienda1
  Authorization: Bearer {jwt}

Response 200:
{
  "items": [
    {
      "productId": "guid",
      "productName": "Camisa Azul",
      "price": 89000,
      "mainImageUrl": "https://...",
      "addedAt": "2026-01-15T14:30:00Z"
    }
  ]
}

POST /me/favorites/{productId}     → 201 Created
DELETE /me/favorites/{productId}   → 204 No Content
```

---

### 🎁 Loyalty (Programa de Puntos)

```http
GET /me/loyalty
Headers:
  X-Tenant-Slug: tienda1
  Authorization: Bearer {jwt}

Response 200:
{
  "accountId": "guid",
  "userId": "guid",
  "currentPoints": 1500,
  "lifetimePoints": 3200,
  "tier": "Silver",
  "joinedAt": "2025-06-15T10:00:00Z"
}

GET /me/loyalty/transactions?page=1&pageSize=20

Response 200:
{
  "items": [
    {
      "id": "guid",
      "type": "EARNED",  # o REDEEMED
      "points": 150,
      "description": "Compra orden ORD-20260120-0001",
      "createdAt": "2026-01-20T10:45:00Z"
    }
  ]
}
```

---

### 🏪 Stores & Inventory (Admin)

```http
GET /api/admin/stores
Headers:
  X-Tenant-Slug: tienda1
  Authorization: Bearer {jwt}

Response 200:
[
  {
    "id": "guid",
    "name": "Tienda Centro",
    "address": "Calle 50 #25-30, Bogotá",
    "phone": "+573001111111",
    "isActive": true,
    "createdAt": "2025-01-10T08:00:00Z"
  }
]

POST /api/admin/stores
{
  "name": "Tienda Norte",
  "address": "Calle 170 #15-20, Bogotá",
  "phone": "+573002222222"
}

GET /api/admin/stores/{storeId}/stock

Response 200:
{
  "storeId": "guid",
  "storeName": "Tienda Centro",
  "products": [
    {
      "productId": "guid",
      "productName": "Camisa Azul",
      "stock": 25,
      "reservedStock": 3,
      "availableStock": 22,
      "updatedAt": "2026-01-20T09:00:00Z"
    }
  ]
}

PUT /api/admin/stores/{storeId}/stock/{productId}
{
  "stock": 30
}
```

---

### 🔐 Admin (Productos, Categorías)

#### **Productos Admin**
```http
GET /api/admin/products?page=1&pageSize=20&isActive=true&categoryId={guid}
Headers:
  X-Tenant-Slug: tienda1
  Authorization: Bearer {jwt}

POST /api/admin/products
{
  "name": "Camisa Nueva",
  "price": 95000,
  "stock": 100,
  "categoryIds": ["guid1", "guid2"],
  "initialStoreStock": [
    { "storeId": "guid", "stock": 50 },
    { "storeId": "guid2", "stock": 50 }
  ]
}

PUT /api/admin/products/{id}
DELETE /api/admin/products/{id}
```

#### **Categorías Admin**
```http
GET /api/admin/categories
POST /api/admin/categories
{
  "name": "Nueva Categoría",
  "slug": "nueva-categoria",
  "parentId": null,
  "displayOrder": 10
}

PUT /api/admin/categories/{id}
DELETE /api/admin/categories/{id}
```

---

### 🚨 ENDPOINTS FALTANTES (Propuesta)

#### **1. Pagos (Wompi)**
```http
# Crear intención de pago
POST /api/payments/wompi/intent
Headers:
  X-Tenant-Slug: tienda1
  Authorization: Bearer {jwt}

Body:
{
  "orderId": "guid",
  "returnUrl": "https://tienda.com/order-confirmation"
}

Response 200:
{
  "paymentIntentId": "wompi-intent-12345",
  "checkoutUrl": "https://checkout.wompi.co/p/12345",
  "orderId": "guid",
  "amount": 216700,
  "currency": "COP"
}

# Verificar pago
GET /api/payments/wompi/{paymentIntentId}/status

Response 200:
{
  "paymentId": "wompi-intent-12345",
  "orderId": "guid",
  "status": "APPROVED",  # PENDING, APPROVED, DECLINED, ERROR
  "amount": 216700,
  "transactionId": "wompi-trans-67890"
}

# Webhook (⚠️ Debe estar excluido de TenantResolutionMiddleware)
POST /webhooks/payments/wompi
Headers:
  X-Wompi-Signature: {hmac-signature}

Body:
{
  "event": "payment.succeeded",  # o payment.failed
  "data": {
    "transactionId": "wompi-trans-67890",
    "orderId": "guid",
    "amount": 216700,
    "status": "APPROVED"
  }
}

Response 200: { "received": true }
```

**⚠️ Importante**:
- Webhook debe validar HMAC signature
- Agregar `/webhooks` a rutas excluidas en TenantResolutionMiddleware
- Tenant se debe extraer del `orderId` (buscar en AdminDb qué tenant tiene esa orden)

---

#### **2. Órdenes Admin**
```http
# Listar todas las órdenes (admin)
GET /api/admin/orders?page=1&pageSize=20&status=PENDING&fromDate=2026-01-01

Response 200:
{
  "items": [
    {
      "id": "guid",
      "orderNumber": "ORD-20260120-0001",
      "userId": "guid",
      "userEmail": "user@example.com",
      "total": 216700,
      "status": "PENDING",
      "createdAt": "2026-01-20T10:30:00Z"
    }
  ],
  "totalCount": 245
}

# Cambiar estado de orden
PUT /api/admin/orders/{orderId}/status
{
  "status": "PROCESSING",  # PENDING, PROCESSING, SHIPPED, DELIVERED, CANCELLED
  "notes": "Orden en preparación"
}

Response 200:
{
  "orderId": "guid",
  "status": "PROCESSING",
  "updatedAt": "2026-01-20T11:00:00Z"
}

# Marcar como enviada
POST /api/admin/orders/{orderId}/ship
{
  "trackingNumber": "COL123456789",
  "carrier": "Servientrega",
  "estimatedDelivery": "2026-01-25"
}

Response 200:
{
  "orderId": "guid",
  "status": "SHIPPED",
  "trackingNumber": "COL123456789",
  "shippedAt": "2026-01-20T11:30:00Z"
}
```

---

#### **3. Reportes (Analytics)**
```http
# Reporte de ventas
GET /api/admin/reports/sales?from=2026-01-01&to=2026-01-31&groupBy=day

Response 200:
{
  "period": {
    "from": "2026-01-01",
    "to": "2026-01-31"
  },
  "totalRevenue": 15000000,
  "totalOrders": 145,
  "averageOrderValue": 103448,
  "data": [
    { "date": "2026-01-01", "revenue": 500000, "orders": 5 },
    { "date": "2026-01-02", "revenue": 750000, "orders": 8 }
  ]
}

# Top productos
GET /api/admin/reports/products/top-sellers?limit=10&period=last30days

Response 200:
[
  {
    "productId": "guid",
    "productName": "Camisa Azul",
    "unitsSold": 145,
    "revenue": 12905000
  }
]
```

---

## 4️⃣ PLAN DE ACCIÓN

### 🚀 Quick Wins (1-2 días)

#### **1. Validar Tenant-User Ownership** ⏱️ 1 día
**Problema**: JWT no incluye `tenantId`, existe riesgo de que usuario de tenant1 acceda a tenant2

**Solución**:
```csharp
// CC.Infraestructure/Tenant/Entities/User.cs
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    // ... otros campos ...
    
    // ⚠️ NO EXISTE ACTUALMENTE - AGREGAR:
    // public Guid TenantId { get; set; }  
}

// Middleware de validación
public class TenantUserOwnershipMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantAccessor tenantAccessor, TenantDbContextFactory dbFactory)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
        {
            await using var db = dbFactory.Create();
            var user = await db.Users.FindAsync(userId);
            
            // ⚠️ VALIDACIÓN CRÍTICA
            if (user?.TenantId != tenantAccessor.TenantInfo.Id)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new { error = "User does not belong to this tenant" });
                return;
            }
        }
        
        await _next(context);
    }
}
```

**Archivos a modificar**:
- `CC.Infraestructure/Tenant/Entities/User.cs` - Agregar `TenantId`
- `CC.Infraestructure/Tenant/TenantDbContext.cs` - Agregar migración
- Nuevo: `Api-eCommerce/Middleware/TenantUserOwnershipMiddleware.cs`
- `Api-eCommerce/Program.cs` - Registrar middleware

---

#### **2. Correlation ID** ⏱️ 4 horas
**Solución**:
```csharp
// Middleware
public class CorrelationIdMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        
        context.Response.Headers.Add("X-Correlation-ID", correlationId);
        
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }
}
```

---

#### **3. JWT Validation Hardening** ⏱️ 2 horas
```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(x => x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,          // ✅ FIX
        ValidIssuer = "ecommerce-api",
        ValidateAudience = true,        // ✅ FIX
        ValidAudience = "ecommerce-pwa",
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(...),
        ClockSkew = TimeSpan.Zero
    });
```

---

#### **4. Tenant Info en JWT** ⏱️ 4 horas
```csharp
// Al generar JWT:
var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim("tenantId", tenantId.ToString()),  // ✅ NUEVO
    new Claim("email", user.Email)
};

// Al validar en middleware:
var jwtTenantId = context.User.FindFirst("tenantId")?.Value;
if (jwtTenantId != tenantAccessor.TenantInfo.Id.ToString())
{
    return Forbid("Token tenant mismatch");
}
```

---

### 🎯 Core Delivery (2 semanas)

#### **Semana 1: Pagos + Webhooks** ⏱️ 5-8 días

**Día 1-2: Integración Wompi SDK**
```
1. Instalar Wompi SDK (NuGet o HTTP client manual)
2. Crear PaymentService:
   - CreatePaymentIntent(orderId, amount, returnUrl)
   - VerifyPayment(paymentIntentId)
   - HandleWebhook(event, signature)
3. Configuración en appsettings.json:
   - Wompi.PublicKey
   - Wompi.PrivateKey
   - Wompi.WebhookSecret
```

**Día 3-4: Endpoints de Pago**
```
1. PaymentController:
   - POST /api/payments/wompi/intent
   - GET /api/payments/wompi/{id}/status
2. Actualizar CheckoutService para integrar con PaymentService
3. Testing con sandbox de Wompi
```

**Día 5: Webhook Handler**
```
1. POST /webhooks/payments/wompi
2. Validar HMAC signature
3. Actualizar OrderService.UpdateOrderStatus()
4. Enviar email de confirmación (si aplica)
5. Agregar /webhooks a rutas excluidas en TenantResolutionMiddleware
```

**Archivos nuevos**:
- `CC.Aplication/Payments/PaymentService.cs`
- `CC.Aplication/Payments/IPaymentService.cs`
- `CC.Aplication/Payments/Wompi/WompiClient.cs`
- `Api-eCommerce/Controllers/PaymentController.cs`
- `Api-eCommerce/Controllers/PaymentWebhookController.cs`

---

#### **Semana 2: Checkout Robusto + Inventario** ⏱️ 5-7 días

**Día 1-2: Stock Reservation**
```sql
-- Nueva tabla
CREATE TABLE StockReservations (
    Id UUID PRIMARY KEY,
    ProductId UUID NOT NULL,
    OrderId UUID NOT NULL,
    Quantity INT NOT NULL,
    ReservedAt TIMESTAMP NOT NULL,
    ExpiresAt TIMESTAMP NOT NULL,
    Status VARCHAR(20) NOT NULL,  -- RESERVED, CONFIRMED, RELEASED
    FOREIGN KEY (ProductId) REFERENCES Products(Id),
    FOREIGN KEY (OrderId) REFERENCES Orders(Id)
);
```

**Día 3-4: CheckoutService Refactor**
```csharp
public async Task<PlaceOrderResponse> PlaceOrderAsync(...)
{
    // 1. Validar stock disponible
    // 2. Crear orden en estado PENDING
    // 3. ✅ NUEVO: Reservar stock (expires in 15 min)
    await _stockService.ReserveStockAsync(orderId, cartItems);
    
    // 4. Crear payment intent
    var payment = await _paymentService.CreatePaymentIntent(orderId, total);
    
    return new PlaceOrderResponse { ... };
}
```

**Día 5: Background Job - Liberar Stock Expirado**
```csharp
public class StockReservationCleanupWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ReleaseExpiredReservations();
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
        }
    }
}
```

**Día 6-7: Testing E2E**
```
1. Test: Crear orden → Reservar stock → Pagar → Confirmar
2. Test: Crear orden → Reservar stock → Timeout → Liberar
3. Test: Race condition (2 users, último item en stock)
```

---

### 🛡️ Hardening (3 semanas)

#### **Semana 3: Observabilidad** ⏱️ 5 días

**Serilog + Structured Logging**
```csharp
// Program.cs
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "eCommerce-API")
        .WriteTo.Console()
        .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
        .WriteTo.Seq("http://seq-server:5341");  // opcional
});
```

**Dashboards**:
- Grafana + Prometheus para métricas
- Seq/ELK para logs
- Application Insights (Azure) para APM

---

#### **Semana 4: Seguridad** ⏱️ 5 días

**Rate Limiting**
```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PerTenant", context =>
    {
        var tenantId = context.HttpContext.GetTenantId();
        var plan = GetTenantPlan(tenantId);
        
        return RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: tenantId,
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = plan.RequestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10
            });
    });
});
```

**IP Whitelisting (Webhooks)**
```csharp
// Middleware
public class WebhookIpWhitelistMiddleware
{
    private static readonly string[] AllowedIPs = { "52.20.123.45", "54.87.65.43" };
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/webhooks"))
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (!AllowedIPs.Contains(remoteIp))
            {
                context.Response.StatusCode = 403;
                return;
            }
        }
        
        await _next(context);
    }
}
```

---

#### **Semana 5: Performance** ⏱️ 5 días

**Redis Distributed Cache**
```csharp
// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ecommerce:";
});

// FeatureCache refactor
public class DistributedFeatureCache : IFeatureCache
{
    private readonly IDistributedCache _cache;
    
    public async Task<bool> IsEnabledAsync(string featureKey)
    {
        var cacheKey = $"feature:{featureKey}";
        var cached = await _cache.GetStringAsync(cacheKey);
        
        if (cached != null)
            return bool.Parse(cached);
        
        var value = await FetchFromDb(featureKey);
        await _cache.SetStringAsync(cacheKey, value.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        });
        
        return value;
    }
}
```

**Query Optimization**
```sql
-- Índices recomendados
CREATE INDEX idx_products_slug ON Products(Slug);
CREATE INDEX idx_products_isactive_isfeatured ON Products(IsActive, IsFeatured);
CREATE INDEX idx_orders_userid_createdat ON Orders(UserId, CreatedAt DESC);
CREATE INDEX idx_orders_status ON Orders(Status);
CREATE INDEX idx_orderitems_orderid ON OrderItems(OrderId);
```

---

## 5️⃣ CHECKLIST DE SEGURIDAD Y MULTITENANCY

### 🔒 Prevención de Tenant Data Leaks

- [ ] **Middleware valida tenant en CADA request**
  - ✅ Implementado: `TenantResolutionMiddleware`
  - ✅ Rutas excluidas correctamente: `/admin`, `/provision`, `/health`, etc.

- [ ] **DbContext siempre usa TenantAccessor**
  - ✅ Implementado: `TenantDbContextFactory` obtiene ConnectionString del tenant

- [ ] **⚠️ Users tienen TenantId y se valida en runtime**
  - ❌ **FALTA**: Agregar campo `TenantId` a tabla `Users`
  - ❌ **FALTA**: Middleware que valide `user.TenantId == tenant actual`
  
  **Implementación**:
  ```csharp
  // Al autenticar usuario (login):
  var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
  if (user.TenantId != _tenantAccessor.TenantInfo.Id)
      throw new UnauthorizedAccessException("User does not belong to this tenant");
  ```

- [ ] **Tests de tenant isolation**
  - ⚠️ **PARCIAL**: Existen algunos tests E2E, faltan tests específicos de isolation
  
  **Test recomendado**:
  ```csharp
  [Fact]
  public async Task User_Cannot_Access_Other_Tenant_Orders()
  {
      // Arrange
      var tenant1 = await CreateTenant("tienda1");
      var tenant2 = await CreateTenant("tienda2");
      var tenant1User = await CreateUser(tenant1);
      var tenant2Order = await CreateOrder(tenant2);
      
      // Act
      var client = CreateAuthenticatedClient(tenant1, tenant1User);
      var response = await client.GetAsync($"/me/orders/{tenant2Order.Id}");
      
      // Assert
      Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }
  ```

- [ ] **AdminDb queries NEVER leak tenant data**
  - ✅ OK: AdminUsers no tienen acceso directo a TenantDb
  - ✅ OK: SuperAdmin ve metadata, NO datos de negocio

- [ ] **Connection strings encriptadas**
  - ✅ Implementado: `TenantConnectionProtector` con DataProtection

---

### 🛡️ Validaciones de Permisos por Tenant

- [ ] **JWT incluye tenantId**
  - ❌ **FALTA**: Actualmente JWT solo tiene `userId`, no `tenantId`
  - **Recomendación**: Agregar claim `tenantId` al generar JWT
  
  ```csharp
  var claims = new List<Claim>
  {
      new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
      new Claim("tenantId", user.TenantId.ToString()),  // ✅ AGREGAR
      new Claim("email", user.Email)
  };
  ```

- [ ] **Permisos por módulo funcionan**
  - ✅ Implementado: `[RequireModule("catalog", "view")]`
  - ✅ Implementado: `ModuleAuthorizationActionFilter`

- [ ] **Feature flags respetan plan del tenant**
  - ✅ Implementado: `FeatureService.IsEnabledAsync()`
  - ✅ Validación por plan + tenant overrides

- [ ] **Plan limits se validan en runtime**
  - ⚠️ **PARCIAL**: `PlanLimitService` existe pero no se usa en todos los endpoints
  - ❌ **FALTA**: Validar límites en:
    - Crear producto → validar `max_products`
    - Crear orden → validar `max_orders_month`
    - Subir imagen → validar `max_storage_mb`
  
  **Implementación**:
  ```csharp
  // En ProductService.CreateAsync():
  await _planLimitService.ValidateLimitAsync("max_products", currentCount + 1);
  ```

---

### 📝 Logging Seguro y Trazabilidad

- [ ] **NO loggear datos sensibles**
  - ⚠️ **REVISAR**: Hacer audit de todos los `_logger.Log*()` para evitar:
    - Passwords
    - Tokens (JWT, API keys)
    - Números de tarjeta
    - Datos personales sensibles (DNI, etc.)
  
  **Ejemplo correcto**:
  ```csharp
  // ❌ MAL
  _logger.LogInformation("User login: {Email} {Password}", email, password);
  
  // ✅ BIEN
  _logger.LogInformation("User login attempt for {Email}", email);
  ```

- [ ] **Logs incluyen tenant context**
  - ⚠️ **PARCIAL**: Algunos logs incluyen tenant, no todos
  - **Recomendación**: Log scope automático en middleware
  
  ```csharp
  // En TenantResolutionMiddleware:
  using (_logger.BeginScope(new Dictionary<string, object>
  {
      ["TenantId"] = tenant.Id,
      ["TenantSlug"] = tenant.Slug
  }))
  {
      await _next(context);
  }
  ```

- [ ] **Correlation ID en todos los logs**
  - ❌ **FALTA**: Implementar `CorrelationIdMiddleware`

- [ ] **Audit log para operaciones críticas**
  - ❌ **FALTA**: Tabla `AuditLog` y logging automático de:
    - CRUD de productos, categorías
    - Cambios de estado de órdenes
    - Modificaciones de usuarios/roles
    - Cambios en plan/features del tenant
  
  **Schema recomendado**:
  ```sql
  CREATE TABLE AuditLog (
      Id UUID PRIMARY KEY,
      TenantId UUID NOT NULL,
      UserId UUID,
      EntityType VARCHAR(100),
      EntityId UUID,
      Action VARCHAR(50),  -- CREATE, UPDATE, DELETE
      OldValues JSONB,
      NewValues JSONB,
      Timestamp TIMESTAMP NOT NULL
  );
  ```

---

### 🔐 Seguridad API

- [ ] **HTTPS en producción**
  - ✅ Configurado: `app.UseHsts()` (fuera de Development)

- [ ] **CORS restrictivo**
  - ✅ Configurado: Lista específica de orígenes permitidos
  - ⚠️ **REVISAR**: Validar que no haya `AllowAnyOrigin()` en producción

- [ ] **Rate limiting global + por tenant**
  - ❌ **FALTA**: No hay rate limiting implementado
  - **Riesgo**: Vulnerable a DDoS, scraping, brute force

- [ ] **Input validation**
  - ✅ Implementado: Data Annotations en DTOs
  - ⚠️ **MEJORAR**: Agregar FluentValidation para reglas más complejas

- [ ] **Output sanitization**
  - ⚠️ **REVISAR**: Validar que respuestas no expongan:
    - Stack traces en producción
    - Información de schema de DB
    - Rutas internas del servidor

- [ ] **Idempotency en operaciones críticas**
  - ✅ Implementado: `PlaceOrder` requiere `Idempotency-Key`
  - ⚠️ **FALTA**: Implementar en:
    - Creación de pagos
    - Actualización de stock (crítico)
    - Cancelación de órdenes

---

### 🧪 Testing de Multitenancy

**Tests recomendados** (faltan implementar):

```csharp
// Tests/Multitenancy/TenantIsolationTests.cs

[Fact]
public async Task Different_Tenants_See_Different_Products()
{
    var tenant1 = await CreateTenant("tienda1");
    var tenant2 = await CreateTenant("tienda2");
    
    await CreateProduct(tenant1, "Producto A");
    await CreateProduct(tenant2, "Producto B");
    
    var tenant1Products = await GetProducts(tenant1);
    var tenant2Products = await GetProducts(tenant2);
    
    Assert.Single(tenant1Products);
    Assert.Equal("Producto A", tenant1Products[0].Name);
    
    Assert.Single(tenant2Products);
    Assert.Equal("Producto B", tenant2Products[0].Name);
}

[Fact]
public async Task User_Cannot_Access_Other_Tenant_Cart()
{
    var tenant1User = CreateJWT(tenantId: tenant1, userId: user1);
    var tenant2Cart = CreateCart(tenantId: tenant2, sessionId: "xyz");
    
    var client = CreateClient(tenant: tenant1, jwt: tenant1User);
    var response = await client.GetAsync("/api/cart", headers: new { X-Tenant-Slug = "tenant2" });
    
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task Admin_User_Cannot_Access_Tenant_Data()
{
    var adminUser = await CreateAdminUser();
    var tenantOrder = await CreateOrder(tenantId: tenant1);
    
    var client = CreateAdminClient(adminUser);
    var response = await client.GetAsync($"/me/orders/{tenantOrder.Id}");
    
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task Provisioning_Creates_Isolated_Database()
{
    var tenant1 = await ProvisionTenant("tienda1");
    var tenant2 = await ProvisionTenant("tienda2");
    
    Assert.NotEqual(tenant1.ConnectionString, tenant2.ConnectionString);
    Assert.Contains("ecom_tenant_tienda1", tenant1.ConnectionString);
    Assert.Contains("ecom_tenant_tienda2", tenant2.ConnectionString);
}
```

---

## 📊 RESUMEN EJECUTIVO

### ✅ Fortalezas Detectadas

1. **Arquitectura sólida de multitenancy**
   - Database-per-tenant con aislamiento físico completo
   - Provisioning automático con background worker
   - Metadata centralizada en AdminDb

2. **Sistema de permisos granular**
   - RBAC (Role-Based Access Control)
   - Permisos por módulo (catalog, orders, inventory, loyalty)
   - Validación mediante atributos y filtros

3. **Feature Flags & Plan Limits**
   - Sistema de planes (Basic, Premium, Enterprise)
   - Feature toggles por tenant
   - Límites configurables (aunque falta validación en runtime)

4. **Multi-location inventory**
   - Sistema de tiendas recién implementado
   - Stock por tienda (ProductStoreStock)
   - Asignación de órdenes a tiendas

5. **Testing E2E básico**
   - Tests de carrito, checkout, catálogo
   - CustomWebApplicationFactory para integration tests

6. **Idempotency**
   - PlaceOrder requiere Idempotency-Key
   - Previene órdenes duplicadas

---

### 🔴 Riesgos Críticos

| Riesgo | Severidad | Impacto | Solución | Esfuerzo |
|--------|-----------|---------|----------|----------|
| **Falta validación tenant-user ownership** | 🔴 Crítica | Data leak entre tenants | Agregar TenantId a Users + middleware | 1-2 días |
| **Integración de pagos incompleta** | 🔴 Alta | Órdenes quedan en PENDING indefinidamente | Integrar Wompi SDK + webhook | 5-8 días |
| **Stock no se reserva durante checkout** | 🟡 Media | Race conditions, overselling | Tabla StockReservations + lógica | 3-4 días |
| **Sin rate limiting** | 🟡 Media | Vulnerable a DDoS, scraping | ASP.NET Rate Limiter | 2-3 días |
| **JWT sin Issuer/Audience validation** | 🟠 Baja | Tokens potencialmente forjables | Habilitar validación | 2 horas |
| **Plan limits no se validan** | 🟠 Baja | Tenants pueden exceder límites | Integrar PlanLimitService | 2 días |

---

### 📈 Métricas de Estado

| Categoría | Completitud | Comentarios |
|-----------|-------------|-------------|
| **Multitenancy** | 85% | DB isolation ✅, user isolation ❌ |
| **Autenticación** | 75% | JWT ✅, tenant validation ❌ |
| **Autorización** | 90% | RBAC + módulos ✅ |
| **Catálogo** | 95% | CRUD completo ✅ |
| **Carrito** | 95% | Session-based ✅ |
| **Checkout** | 70% | Quote ✅, payment integration ❌ |
| **Pagos** | 20% | Solo estructura, sin integración |
| **Órdenes** | 80% | CRUD ✅, admin endpoints ❌ |
| **Inventario** | 85% | Multi-store ✅, reservation ❌ |
| **Loyalty** | 90% | Programa completo ✅ |
| **Security** | 65% | HTTPS ✅, rate limit ❌, audit ❌ |
| **Observabilidad** | 40% | Logs básicos ✅, structured logging ❌ |
| **Testing** | 60% | E2E básico ✅, isolation tests ❌ |

---

### 🎯 Próximos Pasos Recomendados

#### **Prioridad 1 - Esta Semana**
1. ✅ **Validar tenant-user ownership** (1 día)
2. ✅ **Correlation ID** (4 horas)
3. ✅ **JWT hardening** (2 horas)

#### **Prioridad 2 - Próximas 2 Semanas**
1. 💳 **Integración Wompi + Webhook** (5-8 días)
2. 📦 **Stock reservation** (3-4 días)
3. 🚨 **Rate limiting** (2-3 días)

#### **Prioridad 3 - Mes 1**
1. 📧 **Email notifications** (2-3 días)
2. 🔍 **Audit logging** (3-4 días)
3. 📊 **Admin order management** (2 días)
4. 📈 **Basic reports** (3-4 días)

#### **Prioridad 4 - Mes 2+**
1. 📡 **Distributed cache (Redis)** (3-4 días)
2. 📊 **Advanced analytics** (5-7 días)
3. 🔔 **Push notifications** (3-4 días)
4. 🔍 **Full-text search** (3-4 días)

---

### 🛠️ Cambios NO Recomendados (Mantener Compatibilidad)

- ❌ **NO migrar a shared database** - La arquitectura DB-per-tenant es correcta
- ❌ **NO reescribir sistema de permisos** - El actual es robusto
- ❌ **NO cambiar estrategia de tenant resolution** - Header/Query/Host es flexible
- ❌ **NO eliminar endpoints existentes** - Mantener compatibilidad con PWA

---

### 📞 Contacto y Dudas

**Próxima sesión sugerida**: Revisión de cambios implementados + demo de integración de pagos

**Preguntas clave**:
1. ¿Wompi es el único gateway de pago o planean soportar otros?
2. ¿Cuál es el SLA esperado para resolución de tenant? (actualmente sync en middleware)
3. ¿Necesitan facturación automática por uso o solo tracking?
4. ¿Planean escalar horizontalmente (múltiples instancias de API)?

---

**Última actualización**: 20 de enero de 2026  
**Versión del documento**: 1.0  
**Estado del proyecto**: En desarrollo activo
