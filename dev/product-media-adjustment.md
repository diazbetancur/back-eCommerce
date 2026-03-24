# Ajuste de Productos: media por upload + compatibilidad

## Objetivo

Se ajusto el modulo de productos para soportar carga de archivos en el mismo flujo de create/update, con estas reglas:

- Imagen principal por upload (similar a categorias).
- Soporte de multiples imagenes adicionales por producto.
- Soporte de videos por producto.
- Limites por plan aplicados desde el modulo de assets existente.
- Sin romper la logica actual: se mantiene compatibilidad con endpoints JSON existentes.

## Compatibilidad

Se mantienen activos los endpoints actuales JSON:

- `POST /api/admin/products` con `application/json`
- `PUT /api/admin/products/{id}` con `application/json`

Y se agregan variantes `multipart/form-data` sobre las mismas rutas:

- `POST /api/admin/products` con `multipart/form-data`
- `PUT /api/admin/products/{id}` con `multipart/form-data`

ASP.NET selecciona la accion por `Consumes`.

## Reglas de media aplicadas

### Imagen principal

- Campo: `mainImage`
- Si se envia, se sube como `module=product`, `entityType=product`, `entityId=<productId>`.
- Se marca como primaria (`setAsPrimary=true`).

### Imagenes adicionales

- Campo: `images` (multiple)
- Se suben como imagenes no primarias (`setAsPrimary=false`).
- Cuentan para el limite de imagenes por plan.

### Videos

- Campo: `videos` (multiple)
- Se suben como `AssetType=Video`.
- Limite de tamano usado: **5 MB** por archivo (`TenantAssets.MaxVideoBytes=5242880`).
- Si el plan no permite videos o se excede cuota, la carga falla con validacion.

## Limites por plan

Los limites ya existentes del modulo de assets se respetan automaticamente:

- Limite de imagenes por producto (`PlanLimitCodes.MaxProductImages`).
- Limite de videos y almacenamiento total por tenant (snapshot de cuota).
- Tipos/extensiones permitidas segun configuracion `TenantAssets`.

## Campos form-data para create

- `name` (required)
- `sku`
- `description`
- `shortDescription`
- `price` (required)
- `compareAtPrice`
- `stock`
- `trackInventory`
- `isActive`
- `isFeatured`
- `isOnSale`
- `isTaxIncluded`
- `taxPercentage` (nullable, requerido cuando `isTaxIncluded=true`)
- `tags`
- `brand`
- `metaTitle`
- `metaDescription`
- `categoryIds` (repetible)
- `mainImage` (archivo)
- `images` (repetible, archivos)
- `videos` (repetible, archivos)

## Campos form-data para update

Mismos campos de create, todos opcionales excepto las validaciones de negocio.

### Nota de model binding para listas

- `categoryIds` se puede enviar repetido: `categoryIds=value1`, `categoryIds=value2`.
- `initialStoreStock` (si se usa por form) debe ir indexado, por ejemplo:
  - `initialStoreStock[0].storeId=<guid>`
  - `initialStoreStock[0].stock=5`
  - `initialStoreStock[1].storeId=<guid>`
  - `initialStoreStock[1].stock=10`

## Ejemplos

### Create producto con imagen principal + galeria + videos

```bash
curl -X POST "{{baseUrl}}/api/admin/products" \
  -H "Authorization: Bearer {{token}}" \
  -H "X-Tenant-Slug: {{tenantSlug}}" \
  -F "name=Camisa Premium" \
  -F "price=129900" \
  -F "stock=30" \
  -F "trackInventory=true" \
  -F "isActive=true" \
  -F "categoryIds=11111111-1111-1111-1111-111111111111" \
  -F "categoryIds=22222222-2222-2222-2222-222222222222" \
  -F "mainImage=@/ruta/main.jpg" \
  -F "images=@/ruta/gallery-1.jpg" \
  -F "images=@/ruta/gallery-2.jpg" \
  -F "videos=@/ruta/demo.mp4"
```

### Update producto agregando media

```bash
curl -X PUT "{{baseUrl}}/api/admin/products/{{productId}}" \
  -H "Authorization: Bearer {{token}}" \
  -H "X-Tenant-Slug: {{tenantSlug}}" \
  -F "price=139900" \
  -F "mainImage=@/ruta/new-main.jpg" \
  -F "images=@/ruta/new-gallery.jpg"
```

### JSON legacy (se mantiene)

```http
POST {{baseUrl}}/api/admin/products
Authorization: Bearer {{token}}
X-Tenant-Slug: {{tenantSlug}}
Content-Type: application/json

{
  "name": "Producto JSON",
  "price": 99900,
  "stock": 10,
  "trackInventory": true,
  "isActive": true
}
```

## Archivos modificados

- `Api-eCommerce/Controllers/ProductController.cs`

## Nota operativa

Si quieres restringir videos a 3 MB en lugar de 5 MB, basta con ajustar:

- `TenantAssets.MaxVideoBytes` a `3145728` en configuracion.
