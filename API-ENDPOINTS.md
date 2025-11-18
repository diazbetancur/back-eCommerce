# ??? Multi-Tenant eCommerce API - Inventario de Endpoints

## ?? Credenciales por Defecto

**SuperAdmin (Admin Global)**
- Email: `admin@yourdomain.com`
- Password: `Admin123!`
- Endpoint: `POST /admin/auth/login` (sin X-Tenant-Slug)

---

## ?? Tabla de Contenidos

1. [Panel Admin Global](#panel-admin-global-no-requiere-x-tenant-slug)
2. [SuperAdmin - Gestión de Tenants](#superadmin---gestión-de-tenants)
3. [Tenant Auth](#tenant-auth-requiere-x-tenant-slug)
4. [Catálogo](#catálogo-requiere-x-tenant-slug)
5. [Carrito](#carrito-requiere-x-tenant-slug)
6. [Órdenes](#órdenes-requiere-x-tenant-slug)
7. [Favoritos](#favoritos-requiere-x-tenant-slug)
8. [Loyalty](#loyalty-requiere-x-tenant-slug)
9. [Público](#público)

---

## ?? Panel Admin Global (NO requiere X-Tenant-Slug)

### **Auth**

#### Login Admin Global
```http
POST /admin/auth/login
Content-Type: application/json

{
  "email": "admin@yourdomain.com",
  "password": "Admin123!"
}

Response 200:
{
  "token": "eyJhbGc...",
  "expiresAt": "2024-...",
  "user": {
    "id": "...",
    "email": "admin@yourdomain.com",
    "fullName": "Super Administrator",
    "isActive": true,
    "roles": ["SuperAdmin"],
    "createdAt": "...",
    "lastLoginAt": null
  }
}
```

#### Get Admin Profile
```http
GET /admin/auth/me
Authorization: Bearer {token}

Response 200:
{
  "id": "...",
  "email": "admin@yourdomain.com",
  "fullName": "Super Administrator",
  "isActive": true,
  "roles": ["SuperAdmin"],
  "createdAt": "...",
  "lastLoginAt": "..."
}
```

### **Tenants**

#### List Tenants (Paginated)
```http
GET /admin/tenants?page=1&pageSize=20&search=test&status=Ready&planId=...
Authorization: Bearer {token}

Response 200:
{
  "items": [...],
  "totalCount": 10,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

#### Get Tenant by ID
```http
GET /admin/tenants/{tenantId}
Authorization: Bearer {token}

Response 200:
{
  "id": "...",
  "slug": "mi-tienda",
  "name": "Mi Tienda",
  "dbName": "tenant_mi-tienda",
  "status": "Ready",
  "planId": "...",
  "planName": "Premium",
  "featureFlagsJson": null,
  "allowedOrigins": "*",
  "createdAt": "...",
  "updatedAt": "...",
  "lastError": null,
  "recentProvisioningSteps": [...]
}
```

#### Update Tenant
```http
PATCH /admin/tenants/{tenantId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Nuevo Nombre",
  "planId": "...",
  "featureFlagsJson": "{...}",
  "allowedOrigins": "https://example.com",
  "isActive": true
}

Response 200: TenantDetailDto
```

#### Update Tenant Status
```http
PATCH /admin/tenants/{tenantId}/status
Authorization: Bearer {token}
Content-Type: application/json

{
  "status": "Ready"  // Pending, Seeding, Ready, Suspended, Failed
}

Response 200: TenantDetailDto
```

#### Delete Tenant
```http
DELETE /admin/tenants/{tenantId}
Authorization: Bearer {token}

Response 204: No Content
```

---

## ?? SuperAdmin - Gestión de Tenants

### Create Tenant
```http
POST /superadmin/tenants?slug=mi-tienda&name=Mi Tienda&planCode=Basic
Authorization: Bearer {admin_token}

Response 201:
{
  "slug": "mi-tienda",
  "status": "Ready"
}

Nota: Esto crea:
- Base de datos tenant_mi-tienda
- Esquema completo
- Roles (Admin, Manager, Viewer)
- Usuario admin@mi-tienda (password en logs)
```

### Repair Tenant
```http
POST /superadmin/tenants/repair?tenant=mi-tienda
Authorization: Bearer {admin_token}

Response 200:
{
  "tenant": "mi-tienda",
  "status": "Ready"
}
```

---

## ?? Tenant Auth (Requiere X-Tenant-Slug)

### Register
```http
POST /auth/register
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}

Response 200:
{
  "token": "eyJhbGc...",
  "expiresAt": "...",
  "user": {...}
}
```

### Login
```http
POST /auth/login
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!"
}

Response 200:
{
  "token": "eyJhbGc...",
  "expiresAt": "...",
  "user": {
    "id": "...",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "+1234567890",
    "createdAt": "...",
    "isActive": true
  }
}
```

### Get Profile
```http
GET /auth/me
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200: UserProfileDto
```

---

## ?? Catálogo (Requiere X-Tenant-Slug)

### Get Products
```http
GET /products?page=1&pageSize=20&categoryId=...&search=laptop&minPrice=100&maxPrice=1000&sortBy=price&sortOrder=asc
X-Tenant-Slug: mi-tienda

Response 200:
{
  "items": [...],
  "totalCount": 50,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

### Get Product by ID
```http
GET /products/{productId}
X-Tenant-Slug: mi-tienda

Response 200: ProductDto
```

### Get Categories
```http
GET /categories
X-Tenant-Slug: mi-tienda

Response 200: CategoryDto[]
```

---

## ?? Carrito (Requiere X-Tenant-Slug)

### Get Cart
```http
GET /me/cart
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200:
{
  "id": "...",
  "items": [
    {
      "productId": "...",
      "productName": "Laptop",
      "quantity": 2,
      "unitPrice": 1200.00,
      "subtotal": 2400.00
    }
  ],
  "totalItems": 2,
  "totalAmount": 2400.00
}
```

### Add to Cart
```http
POST /me/cart/items
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}
Content-Type: application/json

{
  "productId": "...",
  "quantity": 2
}

Response 200: CartDto
```

### Update Cart Item
```http
PATCH /me/cart/items/{productId}
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}
Content-Type: application/json

{
  "quantity": 3
}

Response 200: CartDto
```

### Remove from Cart
```http
DELETE /me/cart/items/{productId}
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 204: No Content
```

### Clear Cart
```http
DELETE /me/cart
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 204: No Content
```

---

## ?? Órdenes (Requiere X-Tenant-Slug)

### Get User Orders
```http
GET /me/orders?page=1&pageSize=10&status=Completed
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200:
{
  "items": [...],
  "totalCount": 5,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

### Get Order by ID
```http
GET /me/orders/{orderId}
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200: OrderDetailDto
```

---

## ? Favoritos (Requiere X-Tenant-Slug)

### Get Favorites
```http
GET /me/favorites
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200:
{
  "favorites": [
    {
      "productId": "...",
      "productName": "Laptop",
      "price": 1200.00,
      "imageUrl": "...",
      "addedAt": "..."
    }
  ],
  "totalCount": 5
}
```

### Add Favorite
```http
POST /me/favorites
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}
Content-Type: application/json

{
  "productId": "..."
}

Response 200:
{
  "productId": "...",
  "isFavorite": true,
  "addedAt": "..."
}
```

### Remove Favorite
```http
DELETE /me/favorites/{productId}
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 204: No Content
```

### Check Favorite
```http
GET /me/favorites/check/{productId}
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200:
{
  "isFavorite": true
}
```

---

## ?? Loyalty (Requiere X-Tenant-Slug)

### Get Loyalty Account
```http
GET /me/loyalty
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200:
{
  "accountId": "...",
  "userId": "...",
  "currentBalance": 150,
  "totalEarned": 500,
  "totalRedeemed": 350,
  "lastTransaction": {...},
  "createdAt": "..."
}
```

### Get Loyalty Transactions
```http
GET /me/loyalty/transactions?page=1&pageSize=20&type=earned&fromDate=2024-01-01&toDate=2024-12-31
X-Tenant-Slug: mi-tienda
Authorization: Bearer {token}

Response 200:
{
  "items": [...],
  "totalCount": 25,
  "page": 1,
  "pageSize": 20,
  "totalPages": 2
}
```

---

## ?? Público

### Health Check
```http
GET /health

Response 200:
{
  "status": "healthy",
  "timestamp": "2024-..."
}
```

### Get Tenant Config
```http
GET /public/tenant-config
X-Tenant-Slug: mi-tienda

Response 200:
{
  "name": "Mi Tienda",
  "slug": "mi-tienda",
  "theme": {},
  "seo": {},
  "features": ["catalog", "cart", "loyalty", "favorites"]
}
```

---

## ?? Variables de Entorno

```bash
# Base de datos
ConnectionStrings__AdminDb="Host=...;Database=ecommerce_admin;..."

# Template para tenants
Tenancy__TenantDbTemplate="Host=...;Database={DbName};..."

# JWT
jwtKey="tu-clave-secreta-muy-larga"

# Google Storage (opcional)
GoogleStorage__BucketName="..."
```

---

## ?? Comandos Útiles

### Ejecutar proyecto
```bash
cd Api-eCommerce
dotnet run
```

### Crear migración AdminDb
```bash
dotnet ef migrations add MigrationName -p CC.Infraestructure -s Api-eCommerce --context AdminDbContext -o AdminDbMigrations
```

### Crear migración TenantDb
```bash
dotnet ef migrations add MigrationName -p CC.Infraestructure -s Api-eCommerce --context TenantDbContext -o TenantDbMigrations
```

---

## ?? Notas Importantes

### Diferencia Admin Global vs Tenant
- **`/admin/*`** ? NO requiere X-Tenant-Slug, usa AdminDb
- **`/auth/*`, `/products/*`, etc.** ? REQUIERE X-Tenant-Slug, usa TenantDb

### Headers Requeridos
```
Authorization: Bearer {token}       (para endpoints protegidos)
X-Tenant-Slug: mi-tienda           (para endpoints de tenant)
Content-Type: application/json     (para POST/PUT/PATCH)
```

### CORS
Orígenes permitidos:
- `https://pwaecommercee.netlify.app` (producción)
- `http://localhost:4200` (desarrollo)

---

## ?? Flujo Típico

1. **SuperAdmin** login ? `POST /admin/auth/login`
2. **Crear tenant** ? `POST /superadmin/tenants?slug=...`
3. **Usuario** registra ? `POST /auth/register` (con X-Tenant-Slug)
4. **Usuario** login ? `POST /auth/login` (con X-Tenant-Slug)
5. **Agregar al carrito** ? `POST /me/cart/items`
6. **Hacer checkout** ? `POST /checkout`

---

**Última actualización:** 2024
**Versión:** .NET 8
**Base de datos:** PostgreSQL
