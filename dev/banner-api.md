# Banners API (Admin + Storefront)

Guia rapida para consumir el CRUD de banners y el endpoint publico.

## Requisitos

- Header tenant obligatorio: `X-Tenant-Slug: <slug-del-tenant>`
- Para rutas admin: `Authorization: Bearer <jwt>`
- Permisos por modulo `catalog`:
  - `view` para listar/consultar
  - `create` para crear
  - `update` para actualizar
  - `delete` para eliminar

## Enum de posicion

Valores validos para `position`:

- `Hero`
- `Secondary`
- `Sidebar`
- `Popup`
- `Footer`

## 1) Listar banners (Admin)

**GET** `/api/admin/banners?page=1&pageSize=20&search=&position=Hero&isActive=true`

### Ejemplo (HTTP)

```http
GET {{baseUrl}}/api/admin/banners?page=1&pageSize=20&position=Hero&isActive=true
X-Tenant-Slug: {{tenantSlug}}
Authorization: Bearer {{token}}
```

### Respuesta 200 (ejemplo)

```json
{
  "items": [
    {
      "id": "be8f39d6-721f-4cc5-a4fa-4f5ea7f0ee8a",
      "title": "Summer Sale",
      "imageUrl": "https://pub-xxx.r2.dev/tenants/.../banner.jpg",
      "position": "hero",
      "displayOrder": 1,
      "isActive": true,
      "startDate": "2026-03-01T00:00:00Z",
      "endDate": "2026-03-31T23:59:59Z"
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
```

## 2) Obtener banner por Id (Admin)

**GET** `/api/admin/banners/{id}`

### Ejemplo (HTTP)

```http
GET {{baseUrl}}/api/admin/banners/{{bannerId}}
X-Tenant-Slug: {{tenantSlug}}
Authorization: Bearer {{token}}
```

### Respuesta 200 (ejemplo)

```json
{
  "id": "be8f39d6-721f-4cc5-a4fa-4f5ea7f0ee8a",
  "title": "Summer Sale",
  "subtitle": "Hasta 40%",
  "imageUrl": "https://pub-xxx.r2.dev/tenants/.../banner.jpg",
  "targetUrl": "/promos/summer",
  "buttonText": "Comprar",
  "position": "hero",
  "startDate": "2026-03-01T00:00:00Z",
  "endDate": "2026-03-31T23:59:59Z",
  "displayOrder": 1,
  "isActive": true,
  "createdAt": "2026-03-24T14:10:00Z"
}
```

## 3) Crear banner (Admin)

**POST** `/api/admin/banners`

`Content-Type: multipart/form-data`

Campos form-data:

- `title` (required)
- `subtitle` (optional)
- `targetUrl` (optional)
- `buttonText` (optional)
- `position` (optional, default `Hero`)
- `startDate` (optional, ISO-8601)
- `endDate` (optional, ISO-8601)
- `displayOrder` (optional, int)
- `isActive` (optional, bool, default `true`)
- `image` (required, archivo)

### Ejemplo (curl)

```bash
curl -X POST "{{baseUrl}}/api/admin/banners" \
  -H "X-Tenant-Slug: {{tenantSlug}}" \
  -H "Authorization: Bearer {{token}}" \
  -F "title=Summer Sale" \
  -F "subtitle=Hasta 40%" \
  -F "targetUrl=/promos/summer" \
  -F "buttonText=Comprar" \
  -F "position=Hero" \
  -F "startDate=2026-03-01T00:00:00Z" \
  -F "endDate=2026-03-31T23:59:59Z" \
  -F "displayOrder=1" \
  -F "isActive=true" \
  -F "image=@/ruta/local/banner.jpg"
```

### Respuesta 201

Devuelve el objeto `BannerResponse` y header `Location`.

## 4) Actualizar banner (Admin)

**PUT** `/api/admin/banners/{id}`

`Content-Type: multipart/form-data`

Campos form-data:

- mismos campos de create
- `image` es opcional (si no se envia, conserva imagen actual)

### Ejemplo (curl)

```bash
curl -X PUT "{{baseUrl}}/api/admin/banners/{{bannerId}}" \
  -H "X-Tenant-Slug: {{tenantSlug}}" \
  -H "Authorization: Bearer {{token}}" \
  -F "title=Summer Sale Extended" \
  -F "subtitle=Hasta 50%" \
  -F "targetUrl=/promos/summer-extended" \
  -F "buttonText=Ver ofertas" \
  -F "position=Hero" \
  -F "displayOrder=1" \
  -F "isActive=true"
```

### Respuesta 200

Devuelve el objeto `BannerResponse` actualizado.

## 5) Eliminar banner (Admin)

**DELETE** `/api/admin/banners/{id}`

### Ejemplo (HTTP)

```http
DELETE {{baseUrl}}/api/admin/banners/{{bannerId}}
X-Tenant-Slug: {{tenantSlug}}
Authorization: Bearer {{token}}
```

### Respuesta

- `204 No Content`

## 6) Endpoint publico storefront

**GET** `/api/store/banners?position=Hero`

No requiere token, pero si requiere tenant header.

### Ejemplo (HTTP)

```http
GET {{baseUrl}}/api/store/banners?position=Hero
X-Tenant-Slug: {{tenantSlug}}
```

### Respuesta 200 (ejemplo)

```json
[
  {
    "id": "be8f39d6-721f-4cc5-a4fa-4f5ea7f0ee8a",
    "title": "Summer Sale",
    "subtitle": "Hasta 40%",
    "imageUrl": "https://pub-xxx.r2.dev/tenants/.../banner.jpg",
    "targetUrl": "/promos/summer",
    "buttonText": "Comprar",
    "position": "hero"
  }
]
```

## Errores comunes

- `400 Tenant Not Resolved`: falta `X-Tenant-Slug` o el slug no existe.
- `400 Validation Error`: datos invalidos (`title` vacio, rango de fechas invalido, etc.).
- `401/403`: token invalido o sin permisos de modulo `catalog`.
- `404`: banner no existe.
