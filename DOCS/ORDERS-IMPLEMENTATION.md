# ??? Implementación de Órdenes de Usuario y Historial de Compras

## ?? Resumen

Se ha implementado el sistema completo de asociación de órdenes con usuarios autenticados y consulta de historial de compras con las siguientes características:

- ? Asociación automática de órdenes con usuarios autenticados
- ? Soporte para guest checkout (órdenes sin usuario)
- ? Endpoints para consultar historial de órdenes
- ? Detalle de órdenes individuales
- ? Paginación y filtros en historial
- ? Validación de propiedad de órdenes (un usuario solo ve sus propias órdenes)

---

## ??? Arquitectura

### **Modelo de Datos**

La entidad `Order` en TenantDb ya tiene el campo `UserId` opcional:

```csharp
public class Order 
{ 
    public Guid Id { get; set; }
    public string OrderNumber { get; set; }
    public Guid? UserId { get; set; }           // ? FK a UserAccount (opcional)
    public string? SessionId { get; set; }
    public string IdempotencyKey { get; set; }
    public decimal Total { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public string Status { get; set; }
    public string ShippingAddress { get; set; }
    public string Email { get; set; }
    public string? Phone { get; set; }
    public string PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

### **Flujo de Checkout**

#### Con Usuario Autenticado
```
1. Usuario hace login ? Obtiene JWT
2. Usuario agrega productos al carrito (X-Session-Id + Authorization)
3. Usuario hace checkout ? Order.UserId = userId del JWT
4. Orden se asocia automáticamente al usuario
```

#### Sin Usuario (Guest)
```
1. Usuario navega sin autenticarse
2. Usuario agrega productos al carrito (solo X-Session-Id)
3. Usuario hace checkout ? Order.UserId = null
4. Si AllowGuestCheckout = true ? Orden se crea
5. Si AllowGuestCheckout = false ? Retorna 401 Unauthorized
```

---

## ?? API Endpoints

### 1. **GET /me/orders** - Historial de Órdenes

**Endpoint:** `GET /me/orders`  
**Descripción:** Retorna el historial de órdenes del usuario autenticado con paginación y filtros.  
**Autenticación:** ? Requerida (JWT)  
**Headers:**
- `X-Tenant-Slug` (required)
- `Authorization: Bearer {token}` (required)

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `page` | integer | No | 1 | Número de página (1-based) |
| `pageSize` | integer | No | 20 | Items por página (1-100) |
| `status` | string | No | - | Filtrar por estado (PENDING, PROCESSING, SHIPPED, DELIVERED, CANCELLED) |
| `fromDate` | DateTime | No | - | Filtrar desde fecha (ISO 8601) |
| `toDate` | DateTime | No | - | Filtrar hasta fecha (ISO 8601) |

#### Response (200 OK)

```typescript
interface PagedOrdersResponse {
  items: OrderSummaryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface OrderSummaryDto {
  id: string;              // UUID
  orderNumber: string;     // ORD-YYYYMMDD-XXXXXX
  status: string;          // PENDING, PROCESSING, etc.
  total: number;
  createdAt: string;       // ISO 8601
  itemCount: number;       // Number of items in order
}
```

```json
{
  "items": [
    {
      "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
      "orderNumber": "ORD-20241201-123456",
      "status": "PENDING",
      "total": 596.48,
      "createdAt": "2024-12-01T15:30:00Z",
      "itemCount": 3
    },
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "orderNumber": "ORD-20241125-789012",
      "status": "DELIVERED",
      "total": 234.99,
      "createdAt": "2024-11-25T10:15:00Z",
      "itemCount": 1
    }
  ],
  "totalCount": 15,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

#### Error Responses

- `401 Unauthorized` - Token inválido o no proporcionado
- `409 Conflict` - Tenant no resuelto

#### Ejemplos

```bash
# Obtener primera página
curl -X GET "https://api.example.com/me/orders?page=1&pageSize=20" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..."

# Filtrar por estado
curl -X GET "https://api.example.com/me/orders?status=DELIVERED" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..."

# Filtrar por rango de fechas
curl -X GET "https://api.example.com/me/orders?fromDate=2024-11-01T00:00:00Z&toDate=2024-12-01T23:59:59Z" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..."
```

---

### 2. **GET /me/orders/{orderId}** - Detalle de Orden

**Endpoint:** `GET /me/orders/{orderId}`  
**Descripción:** Retorna el detalle completo de una orden específica del usuario.  
**Autenticación:** ? Requerida (JWT)  
**Headers:**
- `X-Tenant-Slug` (required)
- `Authorization: Bearer {token}` (required)

#### Path Parameters

- `orderId` (string, UUID) - ID de la orden

#### Response (200 OK)

```typescript
interface OrderDetailDto {
  id: string;
  orderNumber: string;
  status: string;
  total: number;
  subtotal: number;
  tax: number;
  shipping: number;
  shippingAddress: string;
  email: string;
  phone: string | null;
  paymentMethod: string;
  createdAt: string;           // ISO 8601
  completedAt: string | null;  // ISO 8601
  items: OrderItemDetailDto[];
}

interface OrderItemDetailDto {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  price: number;
  subtotal: number;
}
```

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "orderNumber": "ORD-20241201-123456",
  "status": "PENDING",
  "total": 596.48,
  "subtotal": 509.98,
  "tax": 76.50,
  "shipping": 10.00,
  "shippingAddress": "123 Main St, City, State 12345, Country",
  "email": "customer@example.com",
  "phone": "+1234567890",
  "paymentMethod": "CARD",
  "createdAt": "2024-12-01T15:30:00Z",
  "completedAt": null,
  "items": [
    {
      "id": "item-uuid-1",
      "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "productName": "Wireless Headphones",
      "quantity": 2,
      "price": 254.99,
      "subtotal": 509.98
    }
  ]
}
```

#### Error Responses

- `401 Unauthorized` - Token inválido o no proporcionado
- `404 Not Found` - Orden no encontrada o no pertenece al usuario
- `409 Conflict` - Tenant no resuelto

#### Seguridad

? **Validación de Propiedad:** El sistema verifica que la orden pertenezca al usuario autenticado mediante:
```csharp
var order = await db.Orders
    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
```

Si la orden no existe o pertenece a otro usuario, se retorna `404 Not Found`.

#### Ejemplo

```bash
curl -X GET "https://api.example.com/me/orders/f47ac10b-58cc-4372-a567-0e02b2c3d479" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..."
```

---

## ?? Flujo de Checkout Actualizado

### **POST /api/checkout/place-order**

El endpoint de checkout ahora detecta automáticamente si el usuario está autenticado:

```typescript
// 1. Extrae userId del JWT (si existe)
const userIdClaim = context.User.FindFirst("sub");
const userId = userIdClaim ? Guid.Parse(userIdClaim.Value) : null;

// 2. Si no hay userId y AllowGuestCheckout = false
if (!userId && !allowGuestCheckout) {
    return Results.Unauthorized(); // 401
}

// 3. Crea la orden
const order = await checkoutService.PlaceOrderAsync(sessionId, request, userId);
// Si userId != null ? Order.UserId = userId
// Si userId == null ? Order.UserId = null (guest)
```

### Ejemplo de Checkout Autenticado

```bash
# Checkout con usuario autenticado
curl -X POST "https://api.example.com/api/checkout/place-order" \
  -H "X-Tenant-Slug: my-store" \
  -H "X-Session-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Idempotency-Key: 9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d" \
  -H "Content-Type: application/json" \
  -d '{
    "idempotencyKey": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "email": "customer@example.com",
    "phone": "+1234567890",
    "shippingAddress": "123 Main St, City, State 12345",
    "paymentMethod": "CARD"
  }'
```

### Ejemplo de Guest Checkout

```bash
# Checkout sin autenticación (guest)
curl -X POST "https://api.example.com/api/checkout/place-order" \
  -H "X-Tenant-Slug: my-store" \
  -H "X-Session-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Idempotency-Key: 9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d" \
  -H "Content-Type: application/json" \
  -d '{
    "idempotencyKey": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "email": "guest@example.com",
    "phone": "+1234567890",
    "shippingAddress": "123 Main St",
    "paymentMethod": "CARD"
  }'
```

---

## ?? Ejemplos de Integración

### TypeScript/React Example

```typescript
// 1. Función para obtener historial de órdenes
async function getUserOrders(page: number = 1, status?: string) {
  const token = localStorage.getItem('auth_token');
  
  let url = `https://api.example.com/me/orders?page=${page}&pageSize=20`;
  if (status) {
    url += `&status=${status}`;
  }
  
  const response = await fetch(url, {
    headers: {
      'X-Tenant-Slug': 'my-store',
      'Authorization': `Bearer ${token}`
    }
  });

  if (response.ok) {
    const data = await response.json();
    return data;
  } else if (response.status === 401) {
    // Token expirado, redirigir a login
    window.location.href = '/login';
  } else {
    throw new Error('Failed to fetch orders');
  }
}

// 2. Función para obtener detalle de orden
async function getOrderDetail(orderId: string) {
  const token = localStorage.getItem('auth_token');
  
  const response = await fetch(`https://api.example.com/me/orders/${orderId}`, {
    headers: {
      'X-Tenant-Slug': 'my-store',
      'Authorization': `Bearer ${token}`
    }
  });

  if (response.ok) {
    return await response.json();
  } else if (response.status === 404) {
    throw new Error('Order not found or does not belong to you');
  } else {
    throw new Error('Failed to fetch order details');
  }
}

// 3. Componente React de ejemplo
function OrderHistory() {
  const [orders, setOrders] = useState<PagedOrdersResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState<string | undefined>();

  useEffect(() => {
    loadOrders();
  }, [page, filter]);

  async function loadOrders() {
    try {
      setLoading(true);
      const data = await getUserOrders(page, filter);
      setOrders(data);
    } catch (error) {
      console.error('Error loading orders:', error);
    } finally {
      setLoading(false);
    }
  }

  if (loading) return <div>Loading...</div>;
  if (!orders) return <div>No orders found</div>;

  return (
    <div>
      <h2>Order History</h2>
      
      {/* Filter */}
      <select onChange={(e) => setFilter(e.target.value || undefined)}>
        <option value="">All</option>
        <option value="PENDING">Pending</option>
        <option value="DELIVERED">Delivered</option>
        <option value="CANCELLED">Cancelled</option>
      </select>

      {/* Orders List */}
      <ul>
        {orders.items.map(order => (
          <li key={order.id}>
            <Link to={`/orders/${order.id}`}>
              {order.orderNumber} - ${order.total} - {order.status}
            </Link>
            <span>{new Date(order.createdAt).toLocaleDateString()}</span>
          </li>
        ))}
      </ul>

      {/* Pagination */}
      <div>
        <button 
          disabled={page === 1}
          onClick={() => setPage(page - 1)}
        >
          Previous
        </button>
        <span>Page {page} of {orders.totalPages}</span>
        <button 
          disabled={page === orders.totalPages}
          onClick={() => setPage(page + 1)}
        >
          Next
        </button>
      </div>
    </div>
  );
}

// 4. Checkout con usuario autenticado
async function checkoutWithUser() {
  const token = localStorage.getItem('auth_token');
  const sessionId = localStorage.getItem('session_id');
  const idempotencyKey = crypto.randomUUID();
  
  const response = await fetch('https://api.example.com/api/checkout/place-order', {
    method: 'POST',
    headers: {
      'X-Tenant-Slug': 'my-store',
      'X-Session-Id': sessionId,
      'Authorization': `Bearer ${token}`,  // ? Usuario autenticado
      'Idempotency-Key': idempotencyKey,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      idempotencyKey,
      email: 'user@example.com',
      phone: '+1234567890',
      shippingAddress: '123 Main St',
      paymentMethod: 'CARD'
    })
  });

  const order = await response.json();
  console.log('Order created:', order.orderNumber);
  
  // Redirigir al historial de órdenes
  window.location.href = `/orders/${order.orderId}`;
}
```

---

## ??? Base de Datos

### Relación UserAccount ? Order

```sql
-- La tabla Order ya tiene UserId como FK opcional
ALTER TABLE "Orders" 
ADD CONSTRAINT "FK_Orders_UserAccounts"
FOREIGN KEY ("UserId") 
REFERENCES "UserAccounts" ("Id")
ON DELETE SET NULL;  -- Si se elimina un usuario, las órdenes quedan huérfanas

-- Índice para mejorar performance de consultas
CREATE INDEX "IX_Orders_UserId" ON "Orders" ("UserId");
CREATE INDEX "IX_Orders_UserId_CreatedAt" ON "Orders" ("UserId", "CreatedAt" DESC);
```

### Query de Ejemplo

```sql
-- Obtener órdenes de un usuario con paginación
SELECT o.*, 
       (SELECT COUNT(*) FROM "OrderItems" WHERE "OrderId" = o."Id") AS "ItemCount"
FROM "Orders" o
WHERE o."UserId" = '550e8400-e29b-41d4-a716-446655440000'
ORDER BY o."CreatedAt" DESC
LIMIT 20 OFFSET 0;

-- Órdenes guest (sin usuario)
SELECT * FROM "Orders" WHERE "UserId" IS NULL;

-- Total de órdenes por usuario
SELECT u."Email", COUNT(o."Id") AS "TotalOrders", SUM(o."Total") AS "TotalSpent"
FROM "UserAccounts" u
LEFT JOIN "Orders" o ON u."Id" = o."UserId"
GROUP BY u."Id", u."Email";
```

---

## ? Checklist de Implementación

- [x] DTOs para órdenes (`OrderSummaryDto`, `OrderDetailDto`)
- [x] Servicio `IOrderService` / `OrderService`
- [x] Endpoints `/me/orders` y `/me/orders/{orderId}`
- [x] Validación de propiedad de órdenes (userId match)
- [x] Paginación y filtros en historial
- [x] Actualización de `CheckoutEndpoints` para extraer userId del JWT
- [x] Asociación automática de órdenes con usuarios
- [x] Soporte para guest checkout
- [x] Registro de servicios en `Program.cs`
- [x] Build exitoso

### Pendientes

- [ ] Crear migración para índices en Orders.UserId
- [ ] Testing de endpoints
- [ ] Actualizar README_API.md con nuevos endpoints
- [ ] Implementar filtros adicionales (por fecha, monto, etc.)
- [ ] Agregar endpoint para cancelar órdenes
- [ ] Agregar estadísticas de usuario (total gastado, órdenes completadas, etc.)

---

## ?? Características Clave

### ? Seguridad
- **Aislamiento por Usuario:** Cada usuario solo ve sus propias órdenes
- **Validación en Backend:** No se confía en el cliente
- **JWT Validation:** Token validado en cada request

### ? Rendimiento
- **Paginación:** Evita cargar todas las órdenes de una vez
- **Índices:** Queries optimizadas con índices en UserId y CreatedAt
- **AsNoTracking:** Queries de solo lectura sin tracking de EF Core

### ? Flexibilidad
- **Filtros Opcionales:** Por estado, rango de fechas
- **Guest Checkout:** Soporte para órdenes sin usuario
- **Feature Flags:** Control fino de AllowGuestCheckout

---

## ?? Flujo Completo

```
???????????????????????????????????????????????????????????????
?                    USUARIO SE REGISTRA                      ?
?                  POST /auth/register                        ?
?                         ?                                   ?
?                 Obtiene JWT Token                           ?
???????????????????????????????????????????????????????????????
                           ?
???????????????????????????????????????????????????????????????
?              USUARIO AGREGA PRODUCTOS AL CARRITO            ?
?                  POST /api/cart/items                       ?
?         Headers: X-Tenant-Slug, X-Session-Id,               ?
?                  Authorization: Bearer {token}              ?
???????????????????????????????????????????????????????????????
                           ?
???????????????????????????????????????????????????????????????
?                  USUARIO HACE CHECKOUT                      ?
?              POST /api/checkout/place-order                 ?
?         Headers: X-Tenant-Slug, X-Session-Id,               ?
?                  Authorization: Bearer {token}              ?
?                         ?                                   ?
?    Sistema extrae userId del JWT                            ?
?    Order.UserId = userId                                    ?
?    Orden asociada al usuario                                ?
???????????????????????????????????????????????????????????????
                           ?
???????????????????????????????????????????????????????????????
?           USUARIO CONSULTA SU HISTORIAL DE ÓRDENES          ?
?                   GET /me/orders                            ?
?         Headers: X-Tenant-Slug,                             ?
?                  Authorization: Bearer {token}              ?
?                         ?                                   ?
?    Sistema filtra: WHERE UserId = userId                    ?
?    Retorna solo órdenes del usuario                         ?
???????????????????????????????????????????????????????????????
                           ?
???????????????????????????????????????????????????????????????
?            USUARIO CONSULTA DETALLE DE ORDEN                ?
?              GET /me/orders/{orderId}                       ?
?         Headers: X-Tenant-Slug,                             ?
?                  Authorization: Bearer {token}              ?
?                         ?                                   ?
?    Sistema valida: order.UserId == userId                   ?
?    Retorna detalle si match, 404 si no                      ?
???????????????????????????????????????????????????????????????
```

---

## ?? Próximos Pasos Recomendados

### 1. **Crear Índices de Base de Datos**
```sql
CREATE INDEX "IX_Orders_UserId_CreatedAt" 
ON "Orders" ("UserId", "CreatedAt" DESC);

CREATE INDEX "IX_Orders_UserId_Status" 
ON "Orders" ("UserId", "Status");
```

### 2. **Testing**
```bash
# Test 1: Obtener historial de órdenes
curl -X GET "https://api.example.com/me/orders" \
  -H "X-Tenant-Slug: test-tenant" \
  -H "Authorization: Bearer {valid-token}"

# Test 2: Obtener orden de otro usuario (debe retornar 404)
curl -X GET "https://api.example.com/me/orders/{order-of-other-user}" \
  -H "X-Tenant-Slug: test-tenant" \
  -H "Authorization: Bearer {valid-token}"

# Test 3: Checkout autenticado
curl -X POST "https://api.example.com/api/checkout/place-order" \
  -H "X-Tenant-Slug: test-tenant" \
  -H "X-Session-Id: {session}" \
  -H "Authorization: Bearer {valid-token}" \
  -H "Idempotency-Key: {uuid}" \
  -d '{ /* order data */ }'
```

### 3. **Mejoras Futuras**
- **Tracking de Estado:** Notificar cambios de estado por email
- **Reorden:** Endpoint para repetir una orden anterior
- **Cancelación:** Endpoint para cancelar órdenes PENDING
- **Estadísticas:** Dashboard con total gastado, órdenes completadas, etc.
- **Facturación:** Generar PDFs de facturas
- **Reviews:** Permitir reviews de productos después de DELIVERED

---

**Fecha de implementación:** Diciembre 2024  
**Estado:** ? Completado y listo para testing  
**Build:** ? Exitoso  
**Archivos creados:** 3  
**Archivos modificados:** 3
