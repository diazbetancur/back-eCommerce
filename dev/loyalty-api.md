# Loyalty API - Rewards & Redemptions (Guía funcional)

## Resumen funcional implementado

Se agregó soporte completo para:

- **Vigencia de premio** por ventana: `availableFrom` / `availableUntil`.
- **Cantidad de cupones** mediante `couponQuantity` (alias funcional de `stock`, compatible hacia atrás).
- **Listados con filtros históricos y operativos** para administración.
- **Validación en canje** para impedir redenciones fuera de vigencia.
- **Descuentos con alcance flexible**:
  - general (sin `productIds`),
  - por productos específicos (uno o varios `productIds`),
  - aplicable a todos los elegibles o solo a uno (más costoso / más barato).

## Reglas de negocio clave

1. `availableFrom` y `availableUntil` son opcionales.
   - Si ambos vienen, `availableFrom <= availableUntil`.
2. `couponQuantity` y `stock` pueden enviarse juntos solo si son iguales.
   - Si ambos vienen diferentes: `400 Validation Error`.
3. Redención bloqueada cuando:
   - programa loyalty deshabilitado,
   - premio inactivo,
   - fecha actual `< availableFrom`,
   - fecha actual `> availableUntil`,
   - sin stock,
   - saldo de puntos insuficiente.
4. Eliminación de premio:
   - Si tiene canjes: **soft delete** (`isActive=false`).
   - Si no tiene canjes: **hard delete**.
5. Al consultar dashboard/extracto de customer se ejecuta vencimiento automático:

- se identifican puntos vencidos y se registran como movimiento `EXPIRE` (detalle: `Vencimiento`),
- se descuentan del balance vigente,
- no se incluyen en el conteo de puntos vigentes.

6. `pointsExpiringIn60Days` se calcula sobre puntos aún vigentes con vencimiento en próximos 60 días.
7. Reglas nuevas para descuentos:

- `productIds` vacío/null => descuento general.
- `productIds` con uno o varios IDs => descuento restringido a esos productos.
- `appliesToAllEligibleProducts=true` => aplica a todos los elegibles.
- `appliesToAllEligibleProducts=false` => aplica a un solo producto elegible y exige `singleProductSelectionRule` = `MOST_EXPENSIVE` o `CHEAPEST`.

8. En creación/edición de rewards **no se valida** el `stock` del catálogo contra la cantidad de bonos/cupones configurada en el reward.
9. En requests de creación/edición **ya no se usa** `productId`.
   - Para `rewardType=PRODUCT` se debe enviar `productIds` con **exactamente 1** ID.
   - Si llega más de 1 ID en `productIds` para `PRODUCT`: `400 Validation Error`.
10. Ajuste manual de puntos (`POST /api/admin/loyalty/points/adjust`):

- Si el ajuste es **positivo** (`points > 0`), la expiración se calcula con `pointsExpirationDays` del tenant (si existe).
- Si `pointsExpirationDays` es `null`, el ajuste positivo queda sin fecha de expiración.
- Si el ajuste es **negativo** (`points < 0`), no aplica expiración.
- `points = 0` es inválido (`400 Validation Error`).

---

## Seguridad / permisos por módulo

Todos los endpoints protegidos usan `RequireModule("loyalty", action)` + JWT + tenant resuelto (`X-Tenant-Slug`).

Acciones del módulo loyalty:

- `view`
- `create`
- `update`
- `delete`

### Matriz de permisos por rol (seed por defecto para nuevos tenants)

- **SuperAdmin**: `view/create/update/delete` en todos los módulos.
- **Customer** (módulo loyalty):
  - `view = true`
  - `create = true` (**permite canjear premios**)
  - `update = false`
  - `delete = false`

> Nota: en tenants existentes, los permisos dependen de la data ya sembrada/configurada.

---

## Endpoints Admin (Rewards)

Base: `/api/admin/loyalty/rewards`

### 1) Crear premio

**POST** `/api/admin/loyalty/rewards`  
Permiso: `loyalty:create`

Request (ejemplo):

```json
{
  "name": "Cupón 20%",
  "description": "Descuento por campaña de temporada",
  "pointsCost": 300,
  "rewardType": "DISCOUNT_PERCENTAGE",
  "productIds": ["9fce54e8-2f39-4ee9-a523-41a10d03139d", "4c37e6d2-a524-47a2-a458-65b9307a9e58"],
  "appliesToAllEligibleProducts": false,
  "singleProductSelectionRule": "MOST_EXPENSIVE",
  "discountValue": 20,
  "imageUrl": "https://cdn.miapp.com/rewards/cupon20.png",
  "isActive": true,
  "couponQuantity": 120,
  "validityDays": 15,
  "availableFrom": "2026-03-01T00:00:00Z",
  "availableUntil": "2026-03-31T23:59:59Z",
  "displayOrder": 1
}
```

> Compatibilidad: también acepta `stock`.
>
> Target de producto en request:
>
> - No enviar `productId`.
> - Para `rewardType=PRODUCT`, enviar `productIds` con un único ID.

Response 201 (ejemplo):

```json
{
  "id": "9a4e5a53-2d59-4b28-85fd-a86812cd4cb8",
  "name": "Cupón 20%",
  "description": "Descuento por campaña de temporada",
  "pointsCost": 300,
  "rewardType": "DISCOUNT_PERCENTAGE",
  "productId": null,
  "productIds": ["9fce54e8-2f39-4ee9-a523-41a10d03139d", "4c37e6d2-a524-47a2-a458-65b9307a9e58"],
  "productName": null,
  "appliesToAllEligibleProducts": false,
  "singleProductSelectionRule": "MOST_EXPENSIVE",
  "discountValue": 20,
  "imageUrl": "https://cdn.miapp.com/rewards/cupon20.png",
  "isActive": true,
  "stock": 120,
  "couponQuantity": 120,
  "couponsIssued": 0,
  "couponsAvailable": 120,
  "validityDays": 15,
  "availableFrom": "2026-03-01T00:00:00Z",
  "availableUntil": "2026-03-31T23:59:59Z",
  "isCurrentlyAvailable": false,
  "displayOrder": 1,
  "createdAt": "2026-03-07T15:30:00Z",
  "updatedAt": "2026-03-07T15:30:00Z"
}
```

---

### 2) Actualizar premio

**PUT** `/api/admin/loyalty/rewards/{id}`  
Permiso: `loyalty:update`

Request: mismo contrato de create (`UpdateLoyaltyRewardRequest`).

Ejemplo:

```json
{
  "name": "Cupón 25%",
  "description": "Campaña extendida",
  "pointsCost": 350,
  "rewardType": "DISCOUNT_PERCENTAGE",
  "productIds": ["9fce54e8-2f39-4ee9-a523-41a10d03139d"],
  "appliesToAllEligibleProducts": true,
  "singleProductSelectionRule": null,
  "discountValue": 25,
  "isActive": true,
  "couponQuantity": 100,
  "validityDays": 10,
  "availableFrom": "2026-03-10T00:00:00Z",
  "availableUntil": "2026-04-10T23:59:59Z",
  "displayOrder": 1
}
```

Response 200: `LoyaltyRewardDto` actualizado.

---

### 3) Eliminar premio

**DELETE** `/api/admin/loyalty/rewards/{id}`  
Permiso: `loyalty:delete`

Response:

- `204 No Content` cuando elimina/desactiva correctamente.
- `404` si no existe.

---

### 4) Listar premios con filtros históricos

**GET** `/api/admin/loyalty/rewards`  
Permiso: `loyalty:view`

Query params soport
` (default 1)

- `pageSize` (default 20)
- `isActive` (`true|false`)
- `rewardType` (ej: `PRODUCT`, `DISCOUNT_PERCENTAGE`, `DISCOUNT_FIXED`, `FREE_SHIPPING`)
- `search` (busca en `name` y `description`)
- `availableFrom` (filtra premios con inicio >= valor)
- `availableUntil` (filtra premios con fin <= valor)
- `createdFrom` (fecha creación >= valor)
- `createdTo` (fecha creación <= valor)
- `isCurrentlyAvailable` (`true|false`, según ventana + activo)

Ejemplo:

`GET /api/admin/loyalty/rewards?page=1&pageSize=20&isActive=true&search=cupon&createdFrom=2026-01-01T00:00:00Z&createdTo=2026-12-31T23:59:59Z&isCurrentlyAvailable=true`

Response 200:

```json
{
  "items": [
    {
      "id": "9a4e5a53-2d59-4b28-85fd-a86812cd4cb8",
      "name": "Cupón 25%",
      "description": "Campaña extendida",
      "pointsCost": 350,
      "rewardType": "DISCOUNT_PERCENTAGE",
      "productId": null,
      "productIds": ["9fce54e8-2f39-4ee9-a523-41a10d03139d"],
      "productName": null,
      "appliesToAllEligibleProducts": true,
      "singleProductSelectionRule": null,
      "discountValue": 25,
      "imageUrl": null,
      "isActive": true,
      "stock": 100,
      "couponQuantity": 100,
      "couponsIssued": 37,
      "couponsAvailable": 100,
      "validityDays": 10,
      "availableFrom": "2026-03-10T00:00:00Z",
      "availableUntil": "2026-04-10T23:59:59Z",
      "isCurrentlyAvailable": true,
      "displayOrder": 1,
      "createdAt": "2026-03-07T15:30:00Z",
      "updatedAt": "2026-03-10T00:10:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

---

### 5) Obtener premio por id

**GET** `/api/admin/loyalty/rewards/{id}`  
Permiso: `loyalty:view`

Response 200: `LoyaltyRewardDto`.

---

## Endpoints User (consumo tienda)

Base: `/me/loyalty`

### Dashboard de puntos (vigentes + próximo vencimiento)

**GET** `/me/loyalty`  
Permiso: `loyalty:view`

Incluye:

- `balance`: puntos vigentes (ya excluye vencidos procesados automáticamente).
- `totalEarned`: acumulado histórico de puntos ganados.
- `totalRedeemed`: acumulado histórico de puntos redimidos.
- `pointsExpiringIn60Days`: suma de puntos que vencen en próximos 60 días.
- `lastTransactions`: últimos 5 movimientos para resumen rápido.

Ejemplo:

```json
{
  "balance": 980,
  "totalEarned": 1500,
  "totalRedeemed": 420,
  "pointsExpiringIn60Days": 120,
  "lastTransactions": [
    {
      "id": "ddcbe6b5-6e8c-426f-a97c-0126ab2580c2",
      "type": "EARN",
      "detail": "Acumulación",
      "points": 100,
      "transactionDate": "2026-02-10T14:00:00Z",
      "expirationDate": "2026-04-11T14:00:00Z",
      "description": "Points earned from order ($100.00)",
      "orderNumber": "ORD-1001",
      "createdAt": "2026-02-10T14:00:00Z"
    }
  ]
}
```

### Listar premios canjeables

**GET** `/me/loyalty/rewards?page=1&pageSize=20`  
Público (sin JWT)

Comportamiento:

- Fuerza filtros internos `isActive=true` e `isCurrentlyAvailable=true`.
- No devuelve premios fuera de vigencia.

### Canjear premio

**POST** `/me/loyalty/rewards/{rewardId}/redeem`  
Permiso: `loyalty:create`

Validaciones / flujo de negocio en canje:

- Valida programa loyalty habilitado.
- Valida premio activo y vigente (`availableFrom/availableUntil`).
- Valida puntos suficientes del usuario.
- Crea registro de redención en estado `PENDING`.
- Descuenta puntos del balance del usuario.
- Descuenta `stock`/`couponQuantity` disponible cuando aplica.

Response 200 (`RedeemRewardResponse`):

```json
{
  "redemptionId": "3f2f90ec-3ef0-4cc8-a2ca-cf2da6ffd650",
  "message": "Successfully redeemed Cupón 25%!",
  "remainingPoints": 980,
  "couponCode": "LOYALTY-1A2B3C4D",
  "expiresAt": "2026-03-25T16:00:00Z"
}
```

Errores esperados de negocio (`400`):

- `This reward is not available yet`
- `This reward is no longer available`
- `This reward is out of stock`
- `Insufficient points ...`
- `Loyalty program is disabled for tenant`

### Historial de canjes del usuario

**GET** `/me/loyalty/redemptions?page=1&pageSize=20&status=PENDING`  
Permiso: `loyalty:view`

### Extracto de puntos (movimientos)

**GET** `/me/loyalty/transactions?page=1&pageSize=50&type=EARN|REDEEM|EXPIRE|ADJUST`  
Permiso: `loyalty:view`

Notas:

- Paginado por defecto en **50** registros por página.
- También dispara procesamiento de vencimientos antes de responder.
- Estructura simplificada por movimiento:
  - `detail`: `Acumulación`, `Redención`, `Vencimiento`, `Ajuste (+/-)`.
  - `transactionDate`: fecha de compra/redención; en vencimiento se conserva fecha de compra.
  - `expirationDate`: en redención es el mismo día de la redención; en acumulación/vencimiento viene de `ExpiresAt`.

Ejemplo:

```json
{
  "items": [
    {
      "id": "0a26f684-04f7-4e8c-9f62-f965b909f481",
      "type": "EXPIRE",
      "detail": "Vencimiento",
      "points": -50,
      "transactionDate": "2025-12-01T10:30:00Z",
      "expirationDate": "2026-03-01T10:30:00Z",
      "description": "Points expired from transaction 95f0...",
      "orderNumber": null,
      "createdAt": "2025-12-01T10:30:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "totalPages": 1
}
```

---

## Endpoints Admin (redemptions/config)

- `GET /api/admin/loyalty/dashboard/summary` (`loyalty:view`)
  - resumen para dashboard:
    - ` `: usuarios con actividad loyalty en últimos 6 meses (al menos una transacción o canje registrado).
    - `pointsIssuedCurrentMonth`: suma de puntos positivos emitidos en el mes actual (`EARN` + `ADJUST` positivo).
    - `completedRedemptionsCurrentMonth`: canjes en `DELIVERED` dentro del mes actual.
    - `pendingRedemptionsCurrent`: canjes actualmente en `PENDING`.
- `GET /api/admin/loyalty/redemptions` (`loyalty:view`)
  - filtros: `status`, `userEmail`, `fromDate`, `toDate`, paginación.
  - respuesta por item incluye: `id`, `userId`, `userEmail`, `rewardId`, `rewardName`, `rewardType`, `pointsSpent`, `status`, `couponCode`, `redeemedAt`, `expiresAt`, `deliveredAt`, `adminNotes`, `orderId`, `orderNumber`.
- `PATCH /api/admin/loyalty/redemptions/{id}/status` (`loyalty:update`)
  - body: `{ "status": "APPROVED|DELIVERED|CANCELLED|EXPIRED", "adminNotes": "..." }`.
- `GET /api/admin/loyalty/config` (`loyalty:view`)
- `PUT /api/admin/loyalty/config` (`loyalty:update`)
- `POST /api/admin/loyalty/points/adjust` (`loyalty:create`)
- `GET /api/admin/loyalty/points/adjustments` (`loyalty:view`)
  - filtros: `page`, `pageSize`, `userId`, `adjustedByUserId`, `ticketNumber`, `fromDate`, `toDate`, `search`.
  - devuelve historial de ajustes manuales con: usuario afectado, admin que ajustó, puntos, fecha, observaciones, ticket y expiración.

### Resumen dashboard loyalty

**GET** `/api/admin/loyalty/dashboard/summary`

Permiso: `loyalty:view`

Response 200 (ejemplo):

```json
{
  "generatedAt": "2026-03-10T16:00:00Z",
  "activeUsersWindowStart": "2025-09-10T16:00:00Z",
  "currentMonthStart": "2026-03-01T00:00:00Z",
  "currentMonthEnd": "2026-04-01T00:00:00Z",
  "activeUsersLast6Months": 148,
  "pointsIssuedCurrentMonth": 5230,
  "completedRedemptionsCurrentMonth": 37,
  "pendingRedemptionsCurrent": 12
}
```

### Historial de ajustes manuales (paginado)

**GET** `/api/admin/loyalty/points/adjustments?page=1&pageSize=20&search=diazbetancur@gmail.com&ticketNumber=TCK-2026-00041&fromDate=2026-03-01T00:00:00Z&toDate=2026-03-31T23:59:59Z`

Permiso: `loyalty:view`

Ejemplo de consumo (cURL):

```bash
curl -X GET "http://localhost:5093/api/admin/loyalty/points/adjustments?page=1&pageSize=10&search=diazbetancur@gmail.com" \
  -H "Authorization: Bearer <jwt>" \
  -H "X-Tenant-Slug: <slug>"
```

Response 200 (ejemplo):

```json
{
  "items": [
    {
      "transactionId": "8af8bd41-98ee-4562-86b2-a6bf318f9e9f",
      "userId": "7dbe26fd-d4b6-420c-b8b8-72c951e11ca0",
      "userEmail": "diazbetancur@gmail.com",
      "adjustedByUserId": "f8bf1fdf-78af-426d-b838-b3867a65a5df",
      "adjustedByEmail": "admin@tenant.com",
      "points": 20,
      "transactionType": "ADJUST",
      "observations": "Compensación por incidencia de despacho",
      "ticketNumber": "TCK-2026-00041",
      "expiresAt": "2026-04-08T14:10:00Z",
      "createdAt": "2026-03-09T14:10:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

---

## Requerimientos de integración frontend

Headers mínimos:

- `Authorization: Bearer <jwt>` (endpoints protegidos)
- `X-Tenant-Slug: <slug>`

Sugerencias de UI/UX:

- Usar `isCurrentlyAvailable` para badge "Disponible ahora".
- Mostrar `couponQuantity` como campo principal y mantener `stock` solo por compatibilidad.
- Para reportes históricos admin, usar `createdFrom/createdTo` + `availableFrom/availableUntil` + `search`.

---

## Estado de build/migración

- Build solución: **OK** (solo warnings existentes del proyecto).
- Migración creada: `AddLoyaltyRewardAvailabilityWindow`.
  - agrega columnas `AvailableFrom` y `AvailableUntil` en `LoyaltyRewards`.
  - agrega índices `IX_LoyaltyRewards_AvailableFrom` y `IX_LoyaltyRewards_AvailableUntil`.
- Migración creada: `AddLoyaltyRewardProductScopeRules`.
  - agrega columnas `AppliesToAllEligibleProducts` y `SingleProductSelectionRule` en `LoyaltyRewards`.
  - agrega tabla `LoyaltyRewardProducts` para soportar `productIds` múltiples por reward.
