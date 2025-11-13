# Catálogo, Carrito y Checkout - Guía de Uso

## Descripción General

Sistema completo de eCommerce multi-tenant con:
- **Catálogo**: Productos con búsqueda y paginación
- **Carrito**: Gestión de carrito por sesión (guest) usando X-Session-Id
- **Checkout**: Quote y place-order con idempotencia

Todos los endpoints usan `TenantDbContext` del `TenantDbContextFactory` para acceder a la base de datos del tenant resuelto.

## Headers Requeridos

### Todos los Endpoints
```
X-Tenant-Slug: {slug-del-tenant}
```

### Carrito y Checkout (Guest/Anónimo)
```
X-Session-Id: {guid-unico-por-navegador}
```

## Flujo de Usuario

```
1. Usuario visita tienda ? Frontend genera X-Session-Id
2. GET /api/catalog/products ? Ver productos
3. POST /api/cart/items ? Agregar al carrito (con X-Session-Id)
4. GET /api/cart ? Ver carrito actual
5. POST /api/checkout/quote ? Obtener totales
6. POST /api/checkout/place-order ? Crear pedido (con Idempotency-Key)
```

## Endpoints

### 1. Catálogo

#### GET /api/catalog/products
Lista productos con paginación.

**Headers:**
```
X-Tenant-Slug: acme
```

**Query Parameters:**
- `page` (int, default: 1)
- `pageSize` (int, default: 20)

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/catalog/products?page=1&pageSize=10" \
  -H "X-Tenant-Slug: acme"
```

**Response (200 OK):**
```json
[
  {
    "id": "guid",
    "name": "Producto 1",
    "description": "Descripción del producto",
    "price": 99.99,
    "stock": 50,
    "isActive": true,
    "categories": [],
    "images": []
  }
]
```

#### GET /api/catalog/products/{id}
Obtiene un producto por ID.

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/catalog/products/{product-id}" \
  -H "X-Tenant-Slug: acme"
```

**Response (200 OK):**
```json
{
  "id": "guid",
  "name": "Producto 1",
  "description": "Descripción completa",
  "price": 99.99,
  "stock": 50,
  "isActive": true,
  "categories": [],
  "images": []
}
```

#### GET /api/catalog/products/search
Busca productos por nombre o descripción.

**Query Parameters:**
- `q` (string, requerido): Término de búsqueda

**cURL:**
```bash
curl -X GET "http://localhost:5000/api/catalog/products/search?q=laptop" \
  -H "X-Tenant-Slug: acme"
```

#### POST /api/catalog/products
Crea un nuevo producto.

**Body:**
```json
{
  "name": "Laptop Dell XPS",
  "description": "Laptop de alto rendimiento",
  "price": 1299.99,
  "stock": 10,
  "categoryIds": []
}
```

**cURL:**
```bash
curl -X POST "http://localhost:5000/api/catalog/products" \
  -H "X-Tenant-Slug: acme" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Laptop Dell XPS",
    "description": "Laptop de alto rendimiento",
    "price": 1299.99,
    "stock": 10,
    "categoryIds": []
  }'
```

### 2. Carrito (Shopping Cart)

#### GET /api/cart
Obtiene el carrito actual (crea uno si no existe).

**Headers:**
```
X-Tenant-Slug: acme
X-Session-Id: {tu-session-id}
```

**cURL:**
```bash
SESSION_ID="abc123def456"

curl -X GET "http://localhost:5000/api/cart" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: $SESSION_ID"
```

**Response (200 OK):**
```json
{
  "id": "cart-guid",
  "items": [
    {
      "id": "item-guid",
      "productId": "product-guid",
      "productName": "Laptop Dell XPS",
      "price": 1299.99,
      "quantity": 2,
      "subtotal": 2599.98
    }
  ],
  "subtotal": 2599.98,
  "totalItems": 2
}
```

#### POST /api/cart/items
Agrega un producto al carrito.

**Body:**
```json
{
  "productId": "product-guid",
  "quantity": 2
}
```

**cURL:**
```bash
SESSION_ID="abc123def456"
PRODUCT_ID="product-guid-here"

curl -X POST "http://localhost:5000/api/cart/items" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d "{
    \"productId\": \"$PRODUCT_ID\",
    \"quantity\": 2
  }"
```

**Response (200 OK):** CartDto completo

**Errores:**
- 400: Producto no existe, inactivo o sin stock suficiente

#### PUT /api/cart/items/{itemId}
Actualiza la cantidad de un item.

**Body:**
```json
{
  "quantity": 3
}
```

**cURL:**
```bash
SESSION_ID="abc123def456"
ITEM_ID="item-guid"

curl -X PUT "http://localhost:5000/api/cart/items/$ITEM_ID" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d '{"quantity": 3}'
```

#### DELETE /api/cart/items/{itemId}
Elimina un item del carrito.

**cURL:**
```bash
curl -X DELETE "http://localhost:5000/api/cart/items/$ITEM_ID" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: $SESSION_ID"
```

**Response:** 204 No Content

#### DELETE /api/cart
Vacía el carrito completamente.

**cURL:**
```bash
curl -X DELETE "http://localhost:5000/api/cart" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: $SESSION_ID"
```

### 3. Checkout

#### POST /api/checkout/quote
Obtiene un quote con totales calculados (subtotal, impuesto, envío, total).

**Body:**
```json
{
  "shippingAddress": "Calle Principal 123, Ciudad",
  "email": "cliente@example.com",
  "phone": "+1234567890"
}
```

**cURL:**
```bash
SESSION_ID="abc123def456"

curl -X POST "http://localhost:5000/api/checkout/quote" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": "Calle Principal 123, Ciudad",
    "email": "cliente@example.com",
    "phone": "+1234567890"
  }'
```

**Response (200 OK):**
```json
{
  "subtotal": 2599.98,
  "tax": 389.99,
  "shipping": 0.00,
  "total": 2989.97,
  "items": [
    {
      "id": "item-guid",
      "productId": "product-guid",
      "productName": "Laptop Dell XPS",
      "price": 1299.99,
      "quantity": 2,
      "subtotal": 2599.98
    }
  ]
}
```

**Cálculos:**
- **Tax**: Subtotal × TaxRate (configurado en Settings, default 15%)
- **Shipping**: $10 fijo, gratis si subtotal >= $100
- **Total**: Subtotal + Tax + Shipping

#### POST /api/checkout/place-order
Crea un pedido (requiere Idempotency-Key para evitar duplicados).

**Headers:**
```
X-Tenant-Slug: acme
X-Session-Id: {session-id}
```

**Body:**
```json
{
  "idempotencyKey": "unique-key-per-checkout-attempt",
  "shippingAddress": "Calle Principal 123, Ciudad",
  "email": "cliente@example.com",
  "phone": "+1234567890",
  "paymentMethod": "CARD"
}
```

**Valores válidos para paymentMethod:**
- CARD
- CASH
- TRANSFER

**cURL:**
```bash
SESSION_ID="abc123def456"
IDEMPOTENCY_KEY=$(uuidgen)  # Generar único por intento

curl -X POST "http://localhost:5000/api/checkout/place-order" \
  -H "X-Tenant-Slug: acme" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d "{
    \"idempotencyKey\": \"$IDEMPOTENCY_KEY\",
    \"shippingAddress\": \"Calle Principal 123, Ciudad\",
    \"email\": \"cliente@example.com\",
    \"phone\": \"+1234567890\",
    \"paymentMethod\": \"CARD\"
  }"
```

**Response (201 Created):**
```json
{
  "orderId": "order-guid",
  "orderNumber": "ORD-20250110-123456",
  "total": 2989.97,
  "status": "PENDING",
  "createdAt": "2025-01-10T15:30:00Z"
}
```

**Idempotencia:**
Si se envía el mismo `idempotencyKey` dos veces, se retorna el pedido existente (no se crea duplicado).

## Validaciones Implementadas

### Producto
? Debe existir y estar activo
? Stock suficiente para la cantidad solicitada

### Carrito
? Session ID requerido
? No se permite agregar productos inactivos
? Stock verificado al agregar/actualizar
? Precios actualizados del producto actual

### Checkout
? Carrito no vacío
? Todos los productos deben existir y estar activos
? Stock suficiente para cada item
? Idempotency-Key requerido y único
? Stock se reduce automáticamente al crear pedido
? Carrito se vacía después de crear pedido

## Flujo Completo con cURL

```bash
#!/bin/bash

TENANT="acme"
SESSION_ID=$(uuidgen)
API_URL="http://localhost:5000"

echo "=== eCommerce Multi-Tenant - Flujo Completo ==="
echo "Tenant: $TENANT"
echo "Session ID: $SESSION_ID"
echo ""

# 1. Ver productos
echo "1. Obteniendo catálogo..."
curl -s -X GET "$API_URL/api/catalog/products" \
  -H "X-Tenant-Slug: $TENANT" | jq .

# Guardar ID del primer producto
PRODUCT_ID=$(curl -s -X GET "$API_URL/api/catalog/products" \
  -H "X-Tenant-Slug: $TENANT" | jq -r '.[0].id')

echo "Producto seleccionado: $PRODUCT_ID"
echo ""

# 2. Agregar al carrito
echo "2. Agregando al carrito..."
curl -s -X POST "$API_URL/api/cart/items" \
  -H "X-Tenant-Slug: $TENANT" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d "{\"productId\": \"$PRODUCT_ID\", \"quantity\": 2}" | jq .
echo ""

# 3. Ver carrito
echo "3. Consultando carrito..."
curl -s -X GET "$API_URL/api/cart" \
  -H "X-Tenant-Slug: $TENANT" \
  -H "X-Session-Id: $SESSION_ID" | jq .
echo ""

# 4. Obtener quote
echo "4. Obteniendo quote..."
curl -s -X POST "$API_URL/api/checkout/quote" \
  -H "X-Tenant-Slug: $TENANT" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "shippingAddress": "Calle Principal 123",
    "email": "cliente@example.com",
    "phone": "+1234567890"
  }' | jq .
echo ""

# 5. Crear pedido
echo "5. Creando pedido..."
IDEMPOTENCY_KEY=$(uuidgen)
echo "Idempotency Key: $IDEMPOTENCY_KEY"

ORDER_RESPONSE=$(curl -s -X POST "$API_URL/api/checkout/place-order" \
  -H "X-Tenant-Slug: $TENANT" \
  -H "X-Session-Id: $SESSION_ID" \
  -H "Content-Type: application/json" \
  -d "{
    \"idempotencyKey\": \"$IDEMPOTENCY_KEY\",
    \"shippingAddress\": \"Calle Principal 123, Ciudad\",
    \"email\": \"cliente@example.com\",
    \"phone\": \"+1234567890\",
    \"paymentMethod\": \"CARD\"
  }")

echo "$ORDER_RESPONSE" | jq .
ORDER_ID=$(echo "$ORDER_RESPONSE" | jq -r '.orderId')
echo ""

echo "? Pedido creado: $ORDER_ID"
echo ""

# 6. Verificar que el carrito esté vacío
echo "6. Verificando carrito vacío..."
curl -s -X GET "$API_URL/api/cart" \
  -H "X-Tenant-Slug: $TENANT" \
  -H "X-Session-Id: $SESSION_ID" | jq .
```

## Base de Datos (TenantDbContext)

### Tablas Utilizadas

#### Products
```sql
SELECT * FROM "Products" WHERE "IsActive" = true ORDER BY "Name";
```

#### Carts
```sql
SELECT * FROM "Carts" WHERE "SessionId" = 'abc123';
```

#### CartItems
```sql
SELECT ci.*, p."Name" 
FROM "CartItems" ci
JOIN "Products" p ON ci."ProductId" = p."Id"
WHERE ci."CartId" = 'cart-guid';
```

#### Orders
```sql
SELECT * FROM "Orders" 
WHERE "IdempotencyKey" = 'unique-key' 
OR "OrderNumber" = 'ORD-20250110-123456';
```

#### OrderItems
```sql
SELECT * FROM "OrderItems" WHERE "OrderId" = 'order-guid';
```

## Seguridad

### ? Implementado
- Validación de stock en tiempo real
- Idempotency-Key para evitar pedidos duplicados
- Session ID para carritos guest
- Precios tomados del producto actual (no del frontend)
- Stock reducido transaccionalmente al crear pedido
- Carrito vaciado después de pedido exitoso

### ?? Recomendaciones
- Implementar rate limiting por Session ID
- Agregar autenticación JWT para usuarios registrados
- Validar límites de cantidad por producto
- Implementar timeouts para carritos abandonados
- Agregar webhooks para notificaciones de pedido

## Troubleshooting

### "Tenant slug is required"
**Solución**: Agregar header `X-Tenant-Slug`

### "X-Session-Id header is required" (checkout)
**Solución**: Agregar header `X-Session-Id` (obtenerlo de respuesta de /api/cart)

### "Product not found or inactive"
**Solución**: Verificar que el producto existe y está activo en el tenant

### "Insufficient stock"
**Solución**: Reducir cantidad o esperar restock

### "Cart is empty"
**Solución**: Agregar items al carrito antes de checkout

---

**Autor**: Sistema de IA
**Fecha**: 2025-01-10
**Versión**: 1.0
