# Loyalty Points Payment Config API

## Objetivo

Configurar, por tenant, si se puede usar puntos como dinero y las reglas operativas asociadas, sin afectar por ahora checkout ni carrito.

## Permisos

Si, el endpoint PUT de esta configuracion usa el mismo permiso de actualizacion del modulo loyalty que la configuracion de acumulacion de puntos.

- Configuracion acumulacion: PUT /api/admin/loyalty/config
- Configuracion puntos como dinero: PUT /api/admin/loyalty/points-payment-config
- Permiso en ambos casos: loyalty:update

Tambien aplica:

- GET /api/admin/loyalty/config -> loyalty:view
- GET /api/admin/loyalty/points-payment-config -> loyalty:view

## Endpoints

### 1) Obtener configuracion puntos como dinero

- Metodo: GET
- Ruta: /api/admin/loyalty/points-payment-config
- Permiso: loyalty:view

Headers:

- Authorization: Bearer <TOKEN>
- X-Tenant-Slug: <tenant-slug>

Response 200 (ejemplo):

```json
{
  "isEnabled": true,
  "moneyPerPoint": 5,
  "allowCombineWithCoupons": false,
  "maxMoneyPerTransaction": 200,
  "minimumPayableAmount": 20,
  "currency": "GBP"
}
```

### 2) Actualizar configuracion puntos como dinero

- Metodo: PUT
- Ruta: /api/admin/loyalty/points-payment-config
- Permiso: loyalty:update

Headers:

- Authorization: Bearer <TOKEN>
- X-Tenant-Slug: <tenant-slug>
- Content-Type: application/json

Request body:

```json
{
  "isEnabled": true,
  "moneyPerPoint": 5,
  "allowCombineWithCoupons": false,
  "maxMoneyPerTransaction": 200,
  "minimumPayableAmount": 20
}
```

Response 200:

```json
{
  "isEnabled": true,
  "moneyPerPoint": 5,
  "allowCombineWithCoupons": false,
  "maxMoneyPerTransaction": 200,
  "minimumPayableAmount": 20,
  "currency": "GBP"
}
```

## Reglas de validacion

- moneyPerPoint debe ser mayor que 0.
- maxMoneyPerTransaction no puede ser negativo.
- minimumPayableAmount no puede ser negativo.
- maxMoneyPerTransaction null o 0 significa sin limite.
- currency no se envia en PUT. Se toma desde la moneda configurada del tenant.

## Exposicion al frontend publico

La configuracion tambien se expone en:

- GET /api/public/tenant/{slug}

Bloque nuevo en la respuesta:

```json
{
  "loyaltyPointsPayment": {
    "isEnabled": true,
    "moneyPerPoint": 5,
    "allowCombineWithCoupons": false,
    "maxMoneyPerTransaction": 200,
    "minimumPayableAmount": 20,
    "currency": "GBP"
  }
}
```

## Persistencia interna (Tenant Settings)

Llaves usadas:

- LoyaltyPointsAsMoneyEnabled
- LoyaltyMoneyPerPoint
- LoyaltyAllowCombineWithCoupons
- LoyaltyMaxMoneyPerTransaction
- LoyaltyMinimumPayableAmount

Fallback de compatibilidad:

- Si LoyaltyMoneyPerPoint no existe, se intenta leer LoyaltyPointValue.

## Alcance de esta version

Esta version es solo de configuracion y exposicion de datos.

No aplica aun descuentos por puntos en:

- checkout quote
- place order
- calculo final de pago

## Errores esperados

- 400: error de validacion de campos.
- 401: token ausente o invalido.
- 403: usuario sin permiso de modulo loyalty.
- 409: tenant no resuelto.
- 500: error interno.

## Ejemplos curl

### GET admin config

```bash
curl -X GET "http://localhost:5093/api/admin/loyalty/points-payment-config" \
  -H "Authorization: Bearer <TOKEN>" \
  -H "X-Tenant-Slug: test"
```

### PUT admin config

```bash
curl -X PUT "http://localhost:5093/api/admin/loyalty/points-payment-config" \
  -H "Authorization: Bearer <TOKEN>" \
  -H "X-Tenant-Slug: test" \
  -H "Content-Type: application/json" \
  -d '{
    "isEnabled": true,
    "moneyPerPoint": 5,
    "allowCombineWithCoupons": true,
    "maxMoneyPerTransaction": 0,
    "minimumPayableAmount": 10
  }'
```

### GET public tenant config

```bash
curl -X GET "http://localhost:5093/api/public/tenant/test"
```
