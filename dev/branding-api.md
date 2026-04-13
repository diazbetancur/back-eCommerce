# Branding API (ajuste frontend para logo y favicon por upload)

## Objetivo

Ajustar el formulario de branding para enviar imagenes (logo y favicon) en el mismo endpoint existente,
sin depender de envio de URL desde frontend.

## Cambio aplicado

- Se mantiene la misma ruta de branding:
  - `PATCH /admin/settings/branding`
- Esta ruta ahora espera `multipart/form-data`.
- El frontend ya no debe enviar `logoUrl` ni `faviconUrl`.

## Compatibilidad con tenants existentes

- Los tenants que ya tienen valores URL guardados en settings siguen funcionando igual en lectura.
- La lectura publica y admin sigue usando las mismas llaves:
  - `LogoUrl`
  - `FaviconUrl`
- Cuando se sube una nueva imagen, se reemplaza el valor en esas mismas llaves.

## Endpoint de actualizacion de branding

### Request

- Method: `PATCH`
- Path: `/admin/settings/branding`
- Headers:
  - `Authorization: Bearer <token>`
  - `X-Tenant-Slug: <tenant-slug>`
- Content-Type: `multipart/form-data`

Nota de seguridad para este endpoint:

- En backend se desactivo validacion antiforgery para esta ruta (`DisableAntiforgery`) porque es un endpoint API con JWT y no un formulario MVC con cookie.
- El frontend no debe enviar token antiforgery/CSRF para esta ruta.

### Campos soportados (form-data)

- `logo` (file, opcional)
- `logoFile` (file, opcional, alias de `logo`)
- `favicon` (file, opcional)
- `faviconFile` (file, opcional, alias de `favicon`)
- `primaryColor` (string, opcional)
- `secondaryColor` (string, opcional)
- `accentColor` (string, opcional)
- `backgroundColor` (string, opcional)

Notas:

- Si se envia `logo` y `logoFile`, se toma el primero con contenido valido.
- Si se envia `favicon` y `faviconFile`, se toma el primero con contenido valido.
- Si no se envia archivo para logo o favicon, se conserva el valor actual en DB.

## Campos NO soportados desde frontend (branding)

- `logoUrl`
- `faviconUrl`

## Comportamiento de reemplazo

Para logo y favicon:

1. Se sube la nueva imagen.
2. Si la subida fue exitosa, se eliminan assets anteriores de ese tipo.
3. Se actualiza la misma llave en TenantSettings (`LogoUrl` o `FaviconUrl`) con la URL resultante.

Con esto se evita romper el estado actual y se conserva compatibilidad de lectura.

## Ejemplo cURL

```bash
curl -X PATCH "{{baseUrl}}/admin/settings/branding" \
  -H "Authorization: Bearer {{token}}" \
  -H "X-Tenant-Slug: {{tenantSlug}}" \
  -F "logo=@/ruta/logo.png" \
  -F "favicon=@/ruta/favicon.png" \
  -F "primaryColor=#111827" \
  -F "secondaryColor=#1f2937" \
  -F "accentColor=#22c55e" \
  -F "backgroundColor=#ffffff"
```

## Respuesta esperada (200)

```json
{
  "logoUrl": "https://...",
  "faviconUrl": "https://...",
  "primaryColor": "#111827",
  "secondaryColor": "#1f2937",
  "accentColor": "#22c55e",
  "backgroundColor": "#ffffff"
}
```

## Errores comunes

- `400` si llega archivo vacio para `logo` o `favicon`.
- `409` si no se pudo resolver el tenant.
- `500` en error interno no controlado.

## Checklist frontend

1. Mantener misma ruta: `PATCH /admin/settings/branding`.
2. Cambiar envio a `multipart/form-data`.
3. Enviar archivos en `logo` y `favicon`.
4. Dejar de enviar `logoUrl` y `faviconUrl`.
5. Seguir enviando colores desde el mismo formulario.
6. Validar preview usando `logoUrl` y `faviconUrl` de la respuesta.
