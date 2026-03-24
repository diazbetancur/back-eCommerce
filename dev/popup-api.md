# Popup API (Admin + Public)

## Objetivo

Administrar popups de storefront con estas reglas:

- Solo puede haber 1 popup activo a la vez.
- Se pueden tener todos inactivos.
- Hard delete disponible.
- Fechas `startDate` y `endDate` opcionales.
- Si un popup activo no tiene fechas, aplica siempre.
- Si un popup activo expira (`endDate < now`), se desactiva automaticamente y no se devuelve.

## Admin CRUD

Base: `api/admin/popups`

Requiere:

- `Authorization: Bearer <token>`
- `X-Tenant-Slug: <tenantSlug>`

### Listar

`GET /api/admin/popups?page=1&pageSize=20&isActive=true`

### Obtener por id

`GET /api/admin/popups/{id}`

### Crear (multipart)

`POST /api/admin/popups`

Campos form-data:

- `isActive` (bool, opcional, default false)
- `startDate` (datetime opcional)
- `endDate` (datetime opcional)
- `targetUrl` (string opcional)
- `buttonText` (string opcional)
- `image` (archivo requerido, imagen unica)

### Actualizar (multipart)

`PUT /api/admin/popups/{id}`

Mismos campos de create (incluyendo `image` requerida).

### Eliminar (hard delete)

`DELETE /api/admin/popups/{id}`

## Public endpoint

Base: `api/store`

### Obtener popup vigente

`GET /api/store/popup`

Respuestas:

- `200` con 1 popup cuando aplica.
- `204` cuando no hay ninguno aplicable.

## Ejemplo create

```bash
curl -X POST "{{baseUrl}}/api/admin/popups" \
  -H "Authorization: Bearer {{token}}" \
  -H "X-Tenant-Slug: {{tenantSlug}}" \
  -F "isActive=true" \
  -F "startDate=2026-03-24T12:00:00Z" \
  -F "endDate=2026-04-24T12:00:00Z" \
  -F "targetUrl=https://mi-tienda.com/promocion" \
  -F "buttonText=Ver oferta" \
  -F "image=@/ruta/popup.jpg"
```

## Ejemplo público

```bash
curl -X GET "{{baseUrl}}/api/store/popup" \
  -H "X-Tenant-Slug: {{tenantSlug}}"
```
