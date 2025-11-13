# API Documentation - eCommerce Multi-Tenant Platform

## Base Information

**Base URL:** `https://api.yourdomain.com`  
**Environment:** Production / Staging / Development  
**API Version:** v1  
**Protocol:** HTTPS  
**Content-Type:** `application/json`

---

## Multi-Tenancy & Global Headers

This API implements a **multi-tenant architecture** where each tenant (store) has its own isolated data.

### Required Headers

| Header | Required | Description | Example |
|--------|----------|-------------|---------|
| `X-Tenant-Slug` | ? Yes (most endpoints) | Identifies the tenant/store | `acme-store` |
| `X-Session-Id` | ?? Conditional | Session identifier for guest users (cart, checkout) | `550e8400-e29b-41d4-a716-446655440000` |
| `Authorization` | ?? Conditional | Bearer token for authenticated endpoints | `Bearer eyJhbGc...` |
| `Content-Type` | ? Yes | Always `application/json` | `application/json` |
| `Idempotency-Key` | ?? Conditional | Unique key to prevent duplicate orders | `uuid-v4` |

### Conventions

- **JSON Naming:** camelCase (e.g., `productName`, `totalPrice`)
- **Date Format:** ISO 8601 UTC (e.g., `2024-12-01T15:30:00Z`)
- **IDs:** UUID v4 format
- **Pagination:** Query parameters `page` and `pageSize`

---

## ?? Table of Contents

1. [Tenant Provisioning](#1-tenant-provisioning)
2. [Public Configuration](#2-public-configuration)
3. [Catalog (Public)](#3-catalog-public)
4. [Shopping Cart](#4-shopping-cart)
5. [Checkout](#5-checkout)
6. [Feature Flags](#6-feature-flags)
7. [Super Admin](#7-super-admin)
8. [Health & Observability](#8-health--observability)

---

## 1. Tenant Provisioning

Endpoints for creating and managing new tenant stores.

### 1.1 Initialize Tenant Provisioning

**Endpoint:** `POST /provision/tenants/init`  
**Description:** Creates a new tenant in pending state and returns a confirmation token.  
**Authentication:** None  
**Headers:** None

#### Request Body

```typescript
interface InitProvisioningRequest {
  slug: string;        // Unique identifier (lowercase, alphanumeric + hyphens)
  name: string;        // Display name of the store
  plan: string;        // One of: "Basic", "Premium", "Enterprise"
}
```
```json
{
  "slug": "my-store",
  "name": "My Awesome Store",
  "plan": "Premium"
}
```

#### Response (200 OK)

```typescript
interface InitProvisioningResponse {
  provisioningId: string;    // UUID of the tenant
  confirmToken: string;      // JWT token valid for 15 minutes
  next: string;              // Next endpoint to call
  message: string;           // Confirmation message
}
```
```json
{
  "provisioningId": "550e8400-e29b-41d4-a716-446655440000",
  "confirmToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "next": "/provision/tenants/confirm",
  "message": "Provisioning initialized. Use the confirmation token within 15 minutes to proceed."
}
```

#### Error Responses

- `400 Bad Request` - Invalid plan or slug format
- `409 Conflict` - Slug already exists

---

### 1.2 Confirm Tenant Provisioning

**Endpoint:** `POST /provision/tenants/confirm`  
**Description:** Confirms the provisioning and queues the tenant for creation.  
**Authentication:** Bearer token (from init response)  
**Headers:** `Authorization: Bearer {confirmToken}`

#### Request Body

None (uses token from Authorization header)

#### Response (200 OK)

```typescript
interface ConfirmProvisioningResponse {
  provisioningId: string;
  status: string;              // "QUEUED"
  message: string;
  statusEndpoint: string;      // Endpoint to check status
}
```
```json
{
  "provisioningId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "QUEUED",
  "message": "Provisioning confirmed and queued for processing",
  "statusEndpoint": "/provision/tenants/550e8400-e29b-41d4-a716-446655440000/status"
}
```

#### Error Responses

- `401 Unauthorized` - Invalid or expired token
- `404 Not Found` - Tenant not found
- `400 Bad Request` - Tenant not in pending state

---

### 1.3 Get Provisioning Status

**Endpoint:** `GET /provision/tenants/{provisioningId}/status`  
**Description:** Retrieves the current status of tenant provisioning.  
**Authentication:** None  
**Headers:** None

#### Path Parameters

- `provisioningId` (string, UUID) - The provisioning ID from init response

#### Response (200 OK)

```typescript
interface ProvisioningStepDto {
  step: string;                 // "CreateDatabase", "ApplyMigrations", "SeedData"
  status: string;               // "Pending", "InProgress", "Success", "Failed"
  startedAt: string;            // ISO 8601 date
  completedAt: string | null;   // ISO 8601 date or null
  log: string | null;           // Success message
  errorMessage: string | null;  // Error details if failed
}

interface ProvisioningStatusResponse {
  status: string;               // "Pending", "Seeding", "Ready", "Failed"
  tenantSlug: string | null;    // Available when status is "Ready"
  dbName: string | null;        // Database name when ready
  steps: ProvisioningStepDto[];
}
```
```json
{
  "status": "Ready",
  "tenantSlug": "my-store",
  "dbName": "ecom_tenant_my_store",
  "steps": [
    {
      "step": "CreateDatabase",
      "status": "Success",
      "startedAt": "2024-12-01T10:00:00Z",
      "completedAt": "2024-12-01T10:00:05Z",
      "log": "Database ecom_tenant_my_store created successfully",
      "errorMessage": null
    },
    {
      "step": "ApplyMigrations",
      "status": "Success",
      "startedAt": "2024-12-01T10:00:05Z",
      "completedAt": "2024-12-01T10:00:10Z",
      "log": "Migrations applied successfully",
      "errorMessage": null
    },
    {
      "step": "SeedData",
      "status": "Success",
      "startedAt": "2024-12-01T10:00:10Z",
      "completedAt": "2024-12-01T10:00:15Z",
      "log": "Demo data seeded successfully",
      "errorMessage": null
    }
  ]
}
```

#### Error Responses

- `404 Not Found` - Tenant not found

---

## 2. Public Configuration

### 2.1 Get Tenant Configuration

**Endpoint:** `GET /public/tenant-config`  
**Description:** Retrieves public configuration for the tenant (theme, features, SEO).  
**Authentication:** None  
**Headers:** `X-Tenant-Slug` (required)

#### Response (200 OK)

```typescript
interface TenantConfigResponse {
  name: string;              // Store name
  slug: string;              // Store slug
  theme: object;             // Theme configuration (empty for now)
  seo: object;               // SEO metadata (empty for now)
  features: string[];        // List of enabled feature codes
}
```
```json
{
  "name": "My Awesome Store",
  "slug": "my-store",
  "theme": {},
  "seo": {},
  "features": [
    "catalog",
    "cart",
    "checkout",
    "guest_checkout",
    "categories"
  ]
}
```

#### Error Responses

- `409 Conflict` - Tenant not resolved or not ready

---

## 3. Catalog (Public)

All catalog endpoints are **public** and don't require authentication.

### 3.1 List Products

**Endpoint:** `GET /api/catalog/products`  
**Description:** Retrieves paginated list of active products.  
**Authentication:** None  
**Headers:** `X-Tenant-Slug` (required)

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `page` | integer | No | 1 | Page number (1-based) |
| `pageSize` | integer | No | 20 | Items per page (max 100) |
| `search` | string | No | - | Search in product names |
| `categoryId` | string (UUID) | No | - | Filter by category |
| `minPrice` | decimal | No | - | Minimum price filter |
| `maxPrice` | decimal | No | - | Maximum price filter |

#### Response (200 OK)

```typescript
interface ProductDto {
  id: string;                    // UUID
  name: string;
  description: string;
  price: number;
  discount: number;              // Decimal (0.00 - 1.00)
  finalPrice: number;            // price * (1 - discount)
  stock: number;
  isActive: boolean;
  images: string[];              // Array of image URLs
  categories: CategorySummaryDto[];
  dynamicAttributes: Record<string, any>;  // Key-value pairs
}

interface CategorySummaryDto {
  id: string;
  name: string;
}

interface ProductListResponse {
  items: ProductDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
```
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Wireless Headphones",
      "description": "Premium noise-cancelling headphones",
      "price": 299.99,
      "discount": 0.15,
      "finalPrice": 254.99,
      "stock": 50,
      "isActive": true,
      "images": [
        "https://storage.example.com/products/headphones-1.jpg",
        "https://storage.example.com/products/headphones-2.jpg"
      ],
      "categories": [
        {
          "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
          "name": "Electronics"
        }
      ],
      "dynamicAttributes": {
        "brand": "AudioTech",
        "color": "Black",
        "bluetooth": "5.0"
      }
    }
  ],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

#### Error Responses

- `409 Conflict` - Tenant not resolved

---

### 3.2 Get Product Details

**Endpoint:** `GET /api/catalog/products/{productId}`  
**Description:** Retrieves detailed information for a specific product.  
**Authentication:** None  
**Headers:** `X-Tenant-Slug` (required)

#### Path Parameters

- `productId` (string, UUID) - Product identifier

#### Response (200 OK)

Same structure as `ProductDto` above.

#### Error Responses

- `404 Not Found` - Product not found or inactive
- `409 Conflict` - Tenant not resolved

---

### 3.3 List Categories

**Endpoint:** `GET /api/catalog/categories`  
**Description:** Retrieves all active categories.  
**Authentication:** None  
**Headers:** `X-Tenant-Slug` (required)

#### Response (200 OK)

```typescript
interface CategoryDto {
  id: string;
  name: string;
  description: string;
  productCount: number;    // Number of active products in category
}

type CategoryListResponse = CategoryDto[];
```
```json
[
  {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "name": "Electronics",
    "description": "Electronic devices and accessories",
    "productCount": 45
  },
  {
    "id": "8d0f7780-8536-51ef-c4fd-3d174e01fb08",
    "name": "Clothing",
    "description": "Fashion and apparel",
    "productCount": 120
  }
]
```

---

## 4. Shopping Cart

Cart endpoints support **guest checkout** using session IDs.

### 4.1 Get Cart

**Endpoint:** `GET /api/cart`  
**Description:** Retrieves the current cart for a session.  
**Authentication:** None (guest) or Bearer token (authenticated)  
**Headers:**
- `X-Tenant-Slug` (required)
- `X-Session-Id` (required for guests)

#### Response (200 OK)

```typescript
interface CartItemDto {
  id: string;                   // Cart item ID (not product ID)
  productId: string;            // Product UUID
  productName: string;
  price: number;                // Price at time of adding
  quantity: number;
  subtotal: number;             // price * quantity
  productImage: string | null;
}

interface CartDto {
  id: string;                   // Cart UUID
  sessionId: string;
  items: CartItemDto[];
  itemCount: number;            // Sum of all quantities
  subtotal: number;             // Sum of all subtotals
}
```
```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "items": [
    {
      "id": "item-uuid-1",
      "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "productName": "Wireless Headphones",
      "price": 254.99,
      "quantity": 2,
      "subtotal": 509.98,
      "productImage": "https://storage.example.com/products/headphones-1.jpg"
    }
  ],
  "itemCount": 2,
  "subtotal": 509.98
}
```

#### Error Responses

- `404 Not Found` - Cart not found (empty cart)
- `409 Conflict` - Tenant not resolved

---

### 4.2 Add Item to Cart

**Endpoint:** `POST /api/cart/items`  
**Description:** Adds a product to the cart or updates quantity if already exists.  
**Authentication:** None  
**Headers:**
- `X-Tenant-Slug` (required)
- `X-Session-Id` (required)

#### Request Body

```typescript
interface AddToCartRequest {
  productId: string;    // UUID
  quantity: number;     // Positive integer
}
```
```json
{
  "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "quantity": 1
}
```

#### Response (200 OK)

Returns updated `CartDto` (same structure as Get Cart).

#### Error Responses

- `400 Bad Request` - Invalid product ID or quantity
- `404 Not Found` - Product not found or not active
- `409 Conflict` - Insufficient stock

---

### 4.3 Update Cart Item

**Endpoint:** `PUT /api/cart/items/{itemId}`  
**Description:** Updates the quantity of a cart item.  
**Authentication:** None  
**Headers:**
- `X-Tenant-Slug` (required)
- `X-Session-Id` (required)

#### Path Parameters

- `itemId` (string, UUID) - Cart item identifier (from cart response)

#### Request Body

```typescript
interface UpdateCartItemRequest {
  quantity: number;    // New quantity (must be > 0)
}
```
```json
{
  "quantity": 3
}
```

#### Response (200 OK)

Returns updated `CartDto`.

#### Error Responses

- `404 Not Found` - Cart item not found
- `409 Conflict` - Insufficient stock

---

### 4.4 Remove Cart Item

**Endpoint:** `DELETE /api/cart/items/{itemId}`  
**Description:** Removes an item from the cart.  
**Authentication:** None  
**Headers:**
- `X-Tenant-Slug` (required)
- `X-Session-Id` (required)

#### Path Parameters

- `itemId` (string, UUID) - Cart item identifier

#### Response (204 No Content)

No response body.

#### Error Responses

- `404 Not Found` - Cart item not found

---

### 4.5 Clear Cart

**Endpoint:** `DELETE /api/cart`  
**Description:** Removes all items from the cart.  
**Authentication:** None  
**Headers:**
- `X-Tenant-Slug` (required)
- `X-Session-Id` (required)

#### Response (204 No Content)

No response body.

---

## 5. Checkout

### 5.1 Get Checkout Quote

**Endpoint:** `POST /api/checkout/quote`  
**Description:** Calculates totals, taxes, and shipping for the current cart.  
**Authentication:** None  
**Headers:**
- `X-Tenant-Slug` (required)
- `X-Session-Id` (required)

#### Request Body

```typescript
interface CheckoutQuoteRequest {
  // Empty for now - can be extended with shipping address for accurate calculations
}
```
```json
{}
```

#### Response (200 OK)

```typescript
interface CartItemDto {
  id: string;
  productId: string;
  productName: string;
  price: number;
  quantity: number;
  subtotal: number;
}

interface CheckoutQuoteResponse {
  subtotal: number;       // Sum of all items
  tax: number;            // Calculated tax (based on tenant settings)
  shipping: number;       // Shipping cost (free if > $100)
  total: number;          // subtotal + tax + shipping
  items: CartItemDto[];   // Cart items snapshot
}
```
```json
{
  "subtotal": 509.98,
  "tax": 76.50,
  "shipping": 10.00,
  "total": 596.48,
  "items": [
    {
      "id": "item-uuid-1",
      "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "productName": "Wireless Headphones",
      "price": 254.99,
      "quantity": 2,
      "subtotal": 509.98
    }
  ]
}
```

#### Error Responses

- `400 Bad Request` - Cart is empty
- `409 Conflict` - Product stock changed

---

### 5.2 Place Order

**Endpoint:** `POST /api/checkout/place-order`  
**Description:** Creates an order from the current cart.  
**Authentication:** Optional (guest checkout may be enabled)  
**Headers:**
- `X-Tenant-Slug` (required)
- `X-Session-Id` (required for guests)
- `Authorization: Bearer {token}` (optional, for authenticated users)
- `Idempotency-Key` (required) - UUID v4 to prevent duplicate orders

#### Request Body

```typescript
interface PlaceOrderRequest {
  idempotencyKey: string;      // UUID v4 - must match Idempotency-Key header
  email: string;               // Customer email
  phone: string;               // Customer phone
  shippingAddress: string;     // Full shipping address
  paymentMethod: string;       // "CreditCard", "PayPal", "CashOnDelivery"
}
```
```json
{
  "idempotencyKey": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "email": "customer@example.com",
  "phone": "+1234567890",
  "shippingAddress": "123 Main St, City, State 12345, Country",
  "paymentMethod": "CreditCard"
}
```

#### Response (200 OK)

```typescript
interface PlaceOrderResponse {
  orderId: string;           // UUID
  orderNumber: string;       // Format: ORD-YYYYMMDD-XXXXXX
  total: number;
  status: string;            // "PENDING"
  createdAt: string;         // ISO 8601
}
```
```json
{
  "orderId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "orderNumber": "ORD-20241201-123456",
  "total": 596.48,
  "status": "PENDING",
  "createdAt": "2024-12-01T15:30:00Z"
}
```

#### Error Responses

- `400 Bad Request` - Cart is empty, invalid data, or cart exceeds limits
- `401 Unauthorized` - Guest checkout disabled and no auth token provided
- `409 Conflict` - Insufficient stock, idempotency key conflict (order already exists)

#### Idempotency

If an order with the same `idempotencyKey` already exists, the API returns the existing order (200 OK) instead of creating a duplicate.

---

## 6. Feature Flags

### 6.1 Get Feature Flags

**Endpoint:** `GET /api/features`  
**Description:** Retrieves all feature flags for the current tenant.  
**Authentication:** None  
**Headers:** `X-Tenant-Slug` (required)

#### Response (200 OK)

```typescript
interface FeatureFlagsResponse {
  features: Record<string, boolean>;   // Boolean features
  limits: Record<string, number>;      // Numeric limits
}
```
```json
{
  "features": {
    "catalog": true,
    "cart": true,
    "checkout": true,
    "guestCheckout": true,
    "categories": true,
    "push": false
  },
  "limits": {
    "maxCartItems": 100,
    "maxProductsPerOrder": 50
  }
}
```

---

## 7. Super Admin

Endpoints for super admin operations (manage tenants).

### 7.1 Create Tenant (Direct)

**Endpoint:** `POST /superadmin/tenants`  
**Description:** Creates a tenant directly (bypasses provisioning flow).  
**Authentication:** Super Admin  
**Headers:** None

#### Request Body

```typescript
interface CreateTenantRequest {
  slug: string;
  name: string;
  planCode: string;    // Plan code from database
}
```
```json
{
  "slug": "demo-store",
  "name": "Demo Store",
  "planCode": "PREMIUM"
}
```

#### Response (201 Created)

```typescript
interface CreateTenantResponse {
  slug: string;
  status: string;    // "Ready"
}
```
```json
{
  "slug": "demo-store",
  "status": "Ready"
}
```

#### Error Responses

- `400 Bad Request` - Invalid slug format
- `404 Not Found` - Plan not found
- `409 Conflict` - Slug already exists
- `500 Internal Server Error` - Provisioning failed

---

### 7.2 Repair Tenant

**Endpoint:** `POST /superadmin/tenants/repair`  
**Description:** Repairs a failed tenant (re-applies migrations).  
**Authentication:** Super Admin  
**Headers:** None

#### Request Body

```typescript
interface RepairTenantRequest {
  tenant: string;    // Tenant slug
}
```
```json
{
  "tenant": "demo-store"
}
```

#### Response (200 OK)

```typescript
interface RepairTenantResponse {
  tenant: string;
  status: string;
}
```
```json
{
  "tenant": "demo-store",
  "status": "Ready"
}
```

#### Error Responses

- `404 Not Found` - Tenant not found
- `500 Internal Server Error` - Repair failed

---

## 8. Health & Observability

### 8.1 Health Check

**Endpoint:** `GET /health`  
**Description:** Returns the health status of the API.  
**Authentication:** None  
**Headers:** None

#### Response (200 OK)

```typescript
interface HealthResponse {
  status: string;        // "Healthy"
  timestamp: string;     // ISO 8601
}
```
```json
{
  "status": "Healthy",
  "timestamp": "2024-12-01T15:30:00Z"
}
```

---

## Error Response Format

All errors follow this structure:

```typescript
interface ProblemDetails {
  type?: string;           // URL to error documentation
  title: string;           // Short error title
  status: number;          // HTTP status code
  detail: string;          // Detailed error message
  instance?: string;       // Request path
  errors?: Record<string, string[]>;  // Validation errors
}
```

Example:

```json
{
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "slug": ["Slug must be lowercase alphanumeric with hyphens only"]
  }
}
```

---

## Common HTTP Status Codes

| Code | Description |
|------|-------------|
| `200 OK` | Request successful |
| `201 Created` | Resource created successfully |
| `204 No Content` | Success with no response body |
| `400 Bad Request` | Invalid request data |
| `401 Unauthorized` | Missing or invalid authentication |
| `403 Forbidden` | Feature disabled or quota exceeded |
| `404 Not Found` | Resource not found |
| `409 Conflict` | Conflict (duplicate, insufficient stock, tenant not ready) |
| `423 Locked` | Tenant is locked or suspended |
| `500 Internal Server Error` | Server error |

---

## Integration Examples

### Example 1: Complete Guest Checkout Flow

```typescript
// 1. Get tenant configuration
const configResponse = await fetch('https://api.example.com/public/tenant-config', {
  headers: {
    'X-Tenant-Slug': 'my-store'
  }
});
const config = await configResponse.json();

// 2. Generate session ID (on client)
const sessionId = crypto.randomUUID();

// 3. Browse products
const productsResponse = await fetch('https://api.example.com/api/catalog/products?page=1&pageSize=20', {
  headers: {
    'X-Tenant-Slug': 'my-store'
  }
});
const products = await productsResponse.json();

// 4. Add to cart
const addToCartResponse = await fetch('https://api.example.com/api/cart/items', {
  method: 'POST',
  headers: {
    'X-Tenant-Slug': 'my-store',
    'X-Session-Id': sessionId,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    productId: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    quantity: 2
  })
});
const cart = await addToCartResponse.json();

// 5. Get checkout quote
const quoteResponse = await fetch('https://api.example.com/api/checkout/quote', {
  method: 'POST',
  headers: {
    'X-Tenant-Slug': 'my-store',
    'X-Session-Id': sessionId,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({})
});
const quote = await quoteResponse.json();

// 6. Place order
const idempotencyKey = crypto.randomUUID();
const orderResponse = await fetch('https://api.example.com/api/checkout/place-order', {
  method: 'POST',
  headers: {
    'X-Tenant-Slug': 'my-store',
    'X-Session-Id': sessionId,
    'Idempotency-Key': idempotencyKey,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    idempotencyKey: idempotencyKey,
    email: 'customer@example.com',
    phone: '+1234567890',
    shippingAddress: '123 Main St, City, State 12345',
    paymentMethod: 'CreditCard'
  })
});
const order = await orderResponse.json();
console.log('Order created:', order.orderNumber);
```

---

### Example 2: Tenant Provisioning

```typescript
// 1. Initialize provisioning
const initResponse = await fetch('https://api.example.com/provision/tenants/init', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    slug: 'new-store',
    name: 'New Store',
    plan: 'Premium'
  })
});
const { provisioningId, confirmToken } = await initResponse.json();

// 2. Confirm provisioning
const confirmResponse = await fetch('https://api.example.com/provision/tenants/confirm', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${confirmToken}`,
    'Content-Type': 'application/json'
  }
});
const { statusEndpoint } = await confirmResponse.json();

// 3. Poll status (every 5 seconds)
const pollStatus = async () => {
  const statusResponse = await fetch(`https://api.example.com${statusEndpoint}`);
  const status = await statusResponse.json();
  
  if (status.status === 'Ready') {
    console.log('Tenant ready!', status.tenantSlug);
    return status;
  } else if (status.status === 'Failed') {
    console.error('Provisioning failed:', status.steps);
    throw new Error('Provisioning failed');
  } else {
    console.log('Status:', status.status);
    await new Promise(resolve => setTimeout(resolve, 5000));
    return pollStatus();
  }
};

await pollStatus();
```

---

## Rate Limiting

**Note:** Rate limiting is not currently implemented but is planned for future releases.

Recommended client-side limits:
- Max 100 requests per minute per session
- Max 10 checkout attempts per hour per session

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| v1.0 | 2024-12-01 | Initial API documentation |

---

## Support

For API support, please contact:
- **Email:** api-support@yourdomain.com
- **Documentation:** https://docs.yourdomain.com
- **Status Page:** https://status.yourdomain.com

---

**Last Updated:** December 2024  
**API Version:** 1.0  
**Document Version:** 1.0
