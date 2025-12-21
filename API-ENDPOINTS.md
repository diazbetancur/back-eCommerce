# API Endpoints - Multi-tenant eCommerce

## Tabla de Contenidos
- [Autenticación](#autenticación)
- [Endpoints Públicos](#endpoints-públicos)
- [Tenant Admin Endpoints](#tenant-admin-endpoints)
- [SuperAdmin Endpoints](#superadmin-endpoints)

---

## Autenticación

### Headers Requeridos

| Header | Descripción | Ejemplo |
|--------|-------------|---------|
| `Authorization` | JWT Token (excepto públicos) | `Bearer eyJhbGciOiJIUzI1NiI...` |
| `X-Tenant-Slug` | Identificador del tenant | `mi-tienda` |

---

## Endpoints Públicos

No requieren autenticación.

### GET /api/public/tenant/{slug}
Obtiene la configuración pública de un tenant.

```http
GET /api/public/tenant/mi-tienda HTTP/1.1
Host: localhost:5000
```

**Response:**
```json
{
  "tenant": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "slug": "mi-tienda",
    "displayName": "Mi Tienda",
    "status": "Active",
    "branding": {
      "logoUrl": "https://cdn.example.com/logo.png",
      "faviconUrl": "https://cdn.example.com/favicon.ico",
      "primaryColor": "#3b82f6",
      "secondaryColor": "#1e40af",
      "accentColor": "#10b981"
    }
  },
  "locale": "es-CO",
  "currency": "COP",
  "currencySymbol": "$",
  "features": ["catalog", "cart", "checkout", "loyalty"],
  "contact": {
    "email": "soporte@mitienda.com",
    "phone": "+57 300 123 4567",
    "whatsapp": "573001234567"
  },
  "social": {
    "instagram": "https://instagram.com/mitienda",
    "facebook": "https://facebook.com/mitienda"
  },
  "seo": {
    "title": "Mi Tienda - Los mejores productos",
    "description": "Encuentra los mejores productos en Mi Tienda"
  }
}
```

---

## Tenant Admin Endpoints

Todos requieren:
- Header `Authorization: Bearer {token}`
- Header `X-Tenant-Slug: {tenant-slug}`
- Permiso en el módulo correspondiente

### 🏪 STORE SETTINGS (Módulo: `settings`)

#### GET /admin/settings
Obtiene toda la configuración de la tienda.

**Permiso requerido:** `settings:view`

```http
GET /admin/settings HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
```

**Response:**
```json
{
  "branding": {
    "logoUrl": "https://cdn.example.com/logo.png",
    "faviconUrl": "https://cdn.example.com/favicon.ico",
    "primaryColor": "#3b82f6",
    "secondaryColor": "#1e40af",
    "accentColor": "#10b981",
    "backgroundColor": "#ffffff"
  },
  "contact": {
    "email": "soporte@mitienda.com",
    "phone": "+57 300 123 4567",
    "address": "Calle 123 #45-67, Bogotá",
    "whatsApp": "573001234567"
  },
  "social": {
    "facebook": "https://facebook.com/mitienda",
    "instagram": "https://instagram.com/mitienda",
    "twitter": null,
    "tikTok": null
  },
  "locale": {
    "locale": "es-CO",
    "currency": "COP",
    "currencySymbol": "$",
    "taxRate": 19
  },
  "seo": {
    "title": "Mi Tienda - Los mejores productos",
    "description": "Encuentra los mejores productos en Mi Tienda",
    "keywords": "ecommerce,tienda,productos"
  }
}
```

---

#### PUT /admin/settings
Actualiza toda la configuración (secciones opcionales).

**Permiso requerido:** `settings:update`

```http
PUT /admin/settings HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "branding": {
    "logoUrl": "https://cdn.example.com/nuevo-logo.png",
    "primaryColor": "#ef4444",
    "secondaryColor": "#dc2626",
    "accentColor": "#f59e0b",
    "backgroundColor": "#fef2f2"
  },
  "contact": {
    "email": "nuevo@mitienda.com",
    "phone": "+57 311 999 8888",
    "address": "Nueva dirección #12-34",
    "whatsApp": "573119998888"
  },
  "locale": {
    "locale": "es-CO",
    "currency": "COP",
    "currencySymbol": "$",
    "taxRate": 19
  },
  "seo": {
    "title": "Mi Tienda - Actualizado",
    "description": "Nueva descripción SEO",
    "keywords": "nuevo,keywords,seo"
  }
}
```

---

#### PATCH /admin/settings/branding
Actualiza solo el branding.

**Permiso requerido:** `settings:update`

```http
PATCH /admin/settings/branding HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "logoUrl": "https://cdn.example.com/nuevo-logo.png",
  "faviconUrl": "https://cdn.example.com/nuevo-favicon.ico",
  "primaryColor": "#3b82f6",
  "secondaryColor": "#1e40af",
  "accentColor": "#10b981",
  "backgroundColor": "#ffffff"
}
```

---

#### PATCH /admin/settings/contact
Actualiza solo la información de contacto.

**Permiso requerido:** `settings:update`

```http
PATCH /admin/settings/contact HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "email": "contacto@mitienda.com",
  "phone": "+57 300 123 4567",
  "address": "Calle 123 #45-67, Bogotá, Colombia",
  "whatsApp": "573001234567"
}
```

---

#### PATCH /admin/settings/social
Actualiza solo redes sociales.

**Permiso requerido:** `settings:update`

```http
PATCH /admin/settings/social HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "facebook": "https://facebook.com/mitienda",
  "instagram": "https://instagram.com/mitienda",
  "twitter": "https://twitter.com/mitienda",
  "tikTok": "https://tiktok.com/@mitienda"
}
```

---

### 📦 PRODUCTS (Módulo: `inventory`)

#### GET /admin/products
Lista productos con paginación.

**Permiso requerido:** `inventory:view`

```http
GET /admin/products?page=1&pageSize=20&search=camisa HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
```

---

#### POST /admin/products
Crear nuevo producto.

**Permiso requerido:** `inventory:create`

```http
POST /admin/products HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "name": "Camisa Premium",
  "description": "Camisa de algodón 100%",
  "sku": "CAM-PREM-001",
  "price": 89900,
  "stock": 50,
  "categoryId": "550e8400-e29b-41d4-a716-446655440001"
}
```

---

### 🛒 ORDERS (Módulo: `sales`)

#### GET /admin/orders
Lista órdenes con paginación.

**Permiso requerido:** `sales:view`

```http
GET /admin/orders?page=1&pageSize=20&status=pending HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
```

---

#### PATCH /admin/orders/{id}/status
Actualizar estado de orden.

**Permiso requerido:** `sales:update`

```http
PATCH /admin/orders/550e8400-e29b-41d4-a716-446655440123/status HTTP/1.1
Host: localhost:5000
Authorization: Bearer eyJhbGciOiJIUzI1NiI...
X-Tenant-Slug: mi-tienda
Content-Type: application/json

{
  "status": "shipped",
  "note": "Enviado vía Servientrega"
}
```

---

## Sistema de Permisos

### Módulos Disponibles

| Módulo | Código | Descripción |
|--------|--------|-------------|
| Punto de Venta | `sales` | Gestión de ventas y órdenes |
| Inventario | `inventory` | Gestión de productos y stock |
| Clientes | `customers` | Gestión de usuarios |
| Reportes | `reports` | Analytics y reportes |
| Configuración | `settings` | Configuración de la tienda |
| Fidelización | `loyalty` | Programa de puntos |
| Marketing | `marketing` | Banners y promociones |

### Permisos por Acción

| Permiso | Formato | Descripción |
|---------|---------|-------------|
| View | `{módulo}:view` | Ver/Listar |
| Create | `{módulo}:create` | Crear nuevos |
| Update | `{módulo}:update` | Modificar existentes |
| Delete | `{módulo}:delete` | Eliminar |

### Roles por Defecto

| Rol | Descripción |
|-----|-------------|
| **Admin** | Acceso completo a todos los módulos |
| **Manager** | Operaciones en sales, inventory. Solo lectura en customers, reports |
| **Viewer** | Solo lectura en sales e inventory |

---

## Errores Comunes

| Código | Descripción |
|--------|-------------|
| 401 | No autenticado (token inválido o expirado) |
| 403 | Sin permisos para el módulo/acción |
| 404 | Tenant o recurso no encontrado |
| 400 | Datos inválidos |
| 500 | Error interno |

**Ejemplo error 403:**
```json
{
  "error": "Forbidden",
  "message": "No tienes permiso para view en el módulo settings"
}
```

---

## Postman Collection

Para importar en Postman, usa estas variables de entorno:

```json
{
  "baseUrl": "http://localhost:5000",
  "tenantSlug": "mi-tienda",
  "token": "{{tu-jwt-token}}"
}
```

Y estos headers globales:
```
Authorization: Bearer {{token}}
X-Tenant-Slug: {{tenantSlug}}
Content-Type: application/json
```
