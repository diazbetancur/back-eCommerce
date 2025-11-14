# ?? Implementación de Sistema de Favoritos (Wishlist)

## ?? Resumen

Se ha implementado un sistema completo de favoritos (wishlist) para usuarios autenticados con las siguientes características:

- ? Agregar productos a favoritos
- ? Listar productos favoritos con detalles
- ? Eliminar productos de favoritos
- ? Verificar si un producto es favorito
- ? Operación idempotente (agregar el mismo producto no genera error)
- ? Constraint único (UserId, ProductId) - un usuario no puede tener el mismo producto favorito dos veces
- ? Solo usuarios autenticados pueden usar favoritos

---

## ??? Arquitectura

### **Entidad FavoriteProduct**

```csharp
public class FavoriteProduct
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }        // FK a UserAccount
    public Guid ProductId { get; set; }     // FK a Product
    public DateTime CreatedAt { get; set; }
}
```

### **Constraint Único**

```sql
-- Un usuario no puede tener el mismo producto favorito más de una vez
CREATE UNIQUE INDEX "UQ_FavoriteProducts_UserId_ProductId" 
ON "FavoriteProducts" ("UserId", "ProductId");
```

### **Índices**

```sql
-- Para búsquedas eficientes
CREATE INDEX "IX_FavoriteProducts_UserId" 
ON "FavoriteProducts" ("UserId");

CREATE INDEX "IX_FavoriteProducts_ProductId" 
ON "FavoriteProducts" ("ProductId");
```

---

## ?? API Endpoints

### 1. **GET /me/favorites** - Listar Favoritos

**Endpoint:** `GET /me/favorites`  
**Descripción:** Retorna lista de productos favoritos del usuario autenticado.  
**Autenticación:** ? Requerida (JWT)  
**Headers:**
- `X-Tenant-Slug` (required)
- `Authorization: Bearer {token}` (required)

#### Response (200 OK)

```typescript
interface FavoriteListResponse {
  items: FavoriteProductDto[];
  totalCount: number;
}

interface FavoriteProductDto {
  productId: string;           // UUID
  productName: string;
  price: number;
  mainImageUrl: string | null;
  addedAt: string;             // ISO 8601
  isActive: boolean;           // Si el producto sigue activo
}
```

```json
{
  "items": [
    {
      "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "productName": "Wireless Headphones",
      "price": 254.99,
      "mainImageUrl": "https://storage.example.com/products/headphones.jpg",
      "addedAt": "2024-12-01T15:30:00Z",
      "isActive": true
    },
    {
      "productId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "productName": "Bluetooth Speaker",
      "price": 89.99,
      "mainImageUrl": "https://storage.example.com/products/speaker.jpg",
      "addedAt": "2024-11-28T10:15:00Z",
      "isActive": true
    }
  ],
  "totalCount": 2
}
```

#### Características

- **Ordenamiento:** Por fecha de agregado (más reciente primero)
- **Información del Producto:** Incluye nombre, precio, imagen principal
- **Estado:** Indica si el producto sigue activo
- **Filtrado:** Solo productos que aún existen en el catálogo

#### Error Responses

- `401 Unauthorized` - Token inválido o no proporcionado
- `409 Conflict` - Tenant no resuelto

#### Ejemplo

```bash
curl -X GET "https://api.example.com/me/favorites" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..."
```

---

### 2. **POST /me/favorites** - Agregar a Favoritos

**Endpoint:** `POST /me/favorites`  
**Descripción:** Marca un producto como favorito (operación idempotente).  
**Autenticación:** ? Requerida (JWT)  
**Headers:**
- `X-Tenant-Slug` (required)
- `Authorization: Bearer {token}` (required)

#### Request Body

```typescript
interface AddFavoriteRequest {
  productId: string;  // UUID del producto
}
```

```json
{
  "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

#### Response (200 OK)

```typescript
interface AddFavoriteResponse {
  favoriteId: string;     // UUID del registro de favorito
  productId: string;      // UUID del producto
  message: string;        // Mensaje descriptivo
}
```

```json
{
  "favoriteId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Product added to favorites"
}
```

#### Idempotencia

Si el producto **ya está en favoritos**, la operación retorna 200 OK con el mensaje:

```json
{
  "favoriteId": "existing-favorite-id",
  "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "message": "Product already in favorites"
}
```

? **No genera error**, simplemente confirma que el producto ya está en favoritos.

#### Validaciones

- ? Producto debe existir
- ? Producto debe estar activo (`IsActive = true`)
- ? ProductId debe ser un GUID válido

#### Error Responses

- `400 Bad Request` - ProductId inválido o producto inactivo
- `401 Unauthorized` - Token inválido o no proporcionado
- `404 Not Found` - Producto no encontrado
- `409 Conflict` - Tenant no resuelto

#### Ejemplo

```bash
curl -X POST "https://api.example.com/me/favorites" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{
    "productId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }'
```

---

### 3. **DELETE /me/favorites/{productId}** - Eliminar de Favoritos

**Endpoint:** `DELETE /me/favorites/{productId}`  
**Descripción:** Elimina un producto de los favoritos del usuario.  
**Autenticación:** ? Requerida (JWT)  
**Headers:**
- `X-Tenant-Slug` (required)
- `Authorization: Bearer {token}` (required)

#### Path Parameters

- `productId` (string, UUID) - ID del producto a eliminar

#### Response (204 No Content)

No response body.

#### Error Responses

- `401 Unauthorized` - Token inválido o no proporcionado
- `404 Not Found` - Producto no está en favoritos
- `409 Conflict` - Tenant no resuelto

#### Ejemplo

```bash
curl -X DELETE "https://api.example.com/me/favorites/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..."
```

---

### 4. **GET /me/favorites/check/{productId}** - Verificar si es Favorito

**Endpoint:** `GET /me/favorites/check/{productId}`  
**Descripción:** Verifica si un producto está en los favoritos del usuario.  
**Autenticación:** ? Requerida (JWT)  
**Headers:**
- `X-Tenant-Slug` (required)
- `Authorization: Bearer {token}` (required)

#### Path Parameters

- `productId` (string, UUID) - ID del producto a verificar

#### Response (200 OK)

```typescript
interface CheckFavoriteResponse {
  isFavorite: boolean;
}
```

```json
{
  "isFavorite": true
}
```

#### Uso

Este endpoint es útil para **mostrar el estado del corazón/favorito en la UI** de productos individuales.

#### Ejemplo

```bash
curl -X GET "https://api.example.com/me/favorites/check/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Tenant-Slug: my-store" \
  -H "Authorization: Bearer eyJhbGc..."
```

---

## ?? Flujos de Uso

### **Flujo 1: Agregar y Ver Favoritos**

```
1. Usuario ? Navega catálogo
2. Usuario ? Click en "?? Agregar a Favoritos"
   ??? POST /me/favorites { productId: "..." }
3. Sistema ? Valida JWT
4. Sistema ? Verifica producto existe y está activo
5. Sistema ? Crea FavoriteProduct (o retorna existente)
6. Usuario ? Navega a "Mis Favoritos"
   ??? GET /me/favorites
7. Sistema ? Retorna lista con detalles de productos
```

### **Flujo 2: Toggle Favorito (UI)**

```typescript
// Estado del botón de favorito
const [isFavorite, setIsFavorite] = useState(false);

// Check inicial
useEffect(() => {
  async function checkFavorite() {
    const res = await fetch(`/me/favorites/check/${productId}`, {
      headers: { 'Authorization': `Bearer ${token}` }
    });
    const data = await res.json();
    setIsFavorite(data.isFavorite);
  }
  checkFavorite();
}, [productId]);

// Toggle favorito
async function toggleFavorite() {
  if (isFavorite) {
    // Eliminar
    await fetch(`/me/favorites/${productId}`, {
      method: 'DELETE',
      headers: { 'Authorization': `Bearer ${token}` }
    });
    setIsFavorite(false);
  } else {
    // Agregar
    await fetch('/me/favorites', {
      method: 'POST',
      headers: { 
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ productId })
    });
    setIsFavorite(true);
  }
}
```

---

## ?? Ejemplos de Integración

### TypeScript/React Example

```typescript
// Hook personalizado para favoritos
function useFavorites() {
  const [favorites, setFavorites] = useState<FavoriteListResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const token = localStorage.getItem('auth_token');

  // Cargar favoritos
  async function loadFavorites() {
    setLoading(true);
    try {
      const response = await fetch('https://api.example.com/me/favorites', {
        headers: {
          'X-Tenant-Slug': 'my-store',
          'Authorization': `Bearer ${token}`
        }
      });

      if (response.ok) {
        const data = await response.json();
        setFavorites(data);
      }
    } catch (error) {
      console.error('Error loading favorites:', error);
    } finally {
      setLoading(false);
    }
  }

  // Agregar a favoritos
  async function addFavorite(productId: string) {
    try {
      const response = await fetch('https://api.example.com/me/favorites', {
        method: 'POST',
        headers: {
          'X-Tenant-Slug': 'my-store',
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ productId })
      });

      if (response.ok) {
        await loadFavorites(); // Recargar lista
        return true;
      }
      return false;
    } catch (error) {
      console.error('Error adding favorite:', error);
      return false;
    }
  }

  // Eliminar de favoritos
  async function removeFavorite(productId: string) {
    try {
      const response = await fetch(`https://api.example.com/me/favorites/${productId}`, {
        method: 'DELETE',
        headers: {
          'X-Tenant-Slug': 'my-store',
          'Authorization': `Bearer ${token}`
        }
      });

      if (response.status === 204) {
        await loadFavorites(); // Recargar lista
        return true;
      }
      return false;
    } catch (error) {
      console.error('Error removing favorite:', error);
      return false;
    }
  }

  // Verificar si es favorito
  async function checkFavorite(productId: string): Promise<boolean> {
    try {
      const response = await fetch(`https://api.example.com/me/favorites/check/${productId}`, {
        headers: {
          'X-Tenant-Slug': 'my-store',
          'Authorization': `Bearer ${token}`
        }
      });

      if (response.ok) {
        const data = await response.json();
        return data.isFavorite;
      }
      return false;
    } catch (error) {
      console.error('Error checking favorite:', error);
      return false;
    }
  }

  useEffect(() => {
    if (token) {
      loadFavorites();
    }
  }, [token]);

  return {
    favorites,
    loading,
    addFavorite,
    removeFavorite,
    checkFavorite,
    reload: loadFavorites
  };
}

// Componente de Lista de Favoritos
function FavoritesList() {
  const { favorites, loading, removeFavorite } = useFavorites();

  if (loading) return <Spinner />;
  if (!favorites || favorites.totalCount === 0) {
    return <div>No favorites yet. Start adding products! ??</div>;
  }

  return (
    <div className="favorites-grid">
      <h2>My Favorites ({favorites.totalCount})</h2>
      {favorites.items.map(item => (
        <div key={item.productId} className="product-card">
          <img src={item.mainImageUrl || '/placeholder.jpg'} alt={item.productName} />
          <h3>{item.productName}</h3>
          <p className="price">${item.price}</p>
          {!item.isActive && <span className="badge">Unavailable</span>}
          <button onClick={() => removeFavorite(item.productId)}>
            Remove ??
          </button>
          <Link to={`/products/${item.productId}`}>View Details</Link>
        </div>
      ))}
    </div>
  );
}

// Componente de Botón de Favorito
function FavoriteButton({ productId }: { productId: string }) {
  const { checkFavorite, addFavorite, removeFavorite } = useFavorites();
  const [isFavorite, setIsFavorite] = useState(false);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    async function check() {
      const result = await checkFavorite(productId);
      setIsFavorite(result);
    }
    check();
  }, [productId]);

  async function toggle() {
    setLoading(true);
    try {
      if (isFavorite) {
        const success = await removeFavorite(productId);
        if (success) setIsFavorite(false);
      } else {
        const success = await addFavorite(productId);
        if (success) setIsFavorite(true);
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <button 
      onClick={toggle} 
      disabled={loading}
      className={isFavorite ? 'favorite active' : 'favorite'}
    >
      {isFavorite ? '??' : '??'} {isFavorite ? 'In Favorites' : 'Add to Favorites'}
    </button>
  );
}
```

---

## ??? Base de Datos

### **Tabla FavoriteProducts**

```sql
CREATE TABLE "FavoriteProducts" (
    "Id" UUID PRIMARY KEY,
    "UserId" UUID NOT NULL,
    "ProductId" UUID NOT NULL,
    "CreatedAt" TIMESTAMP NOT NULL,
    
    -- Foreign Keys
    CONSTRAINT "FK_FavoriteProducts_UserAccounts" 
        FOREIGN KEY ("UserId") 
        REFERENCES "UserAccounts" ("Id") 
        ON DELETE CASCADE,
    
    CONSTRAINT "FK_FavoriteProducts_Products" 
        FOREIGN KEY ("ProductId") 
        REFERENCES "Products" ("Id") 
        ON DELETE CASCADE
);

-- Constraint único: un usuario no puede tener el mismo producto dos veces
CREATE UNIQUE INDEX "UQ_FavoriteProducts_UserId_ProductId" 
ON "FavoriteProducts" ("UserId", "ProductId");

-- Índices para búsquedas
CREATE INDEX "IX_FavoriteProducts_UserId" 
ON "FavoriteProducts" ("UserId");

CREATE INDEX "IX_FavoriteProducts_ProductId" 
ON "FavoriteProducts" ("ProductId");
```

### **Queries de Ejemplo**

```sql
-- Obtener favoritos de un usuario
SELECT fp.*, p."Name", p."Price", pi."ImageUrl"
FROM "FavoriteProducts" fp
INNER JOIN "Products" p ON fp."ProductId" = p."Id"
LEFT JOIN "ProductImages" pi ON p."Id" = pi."ProductId" AND pi."IsPrimary" = true
WHERE fp."UserId" = '550e8400-e29b-41d4-a716-446655440000'
ORDER BY fp."CreatedAt" DESC;

-- Verificar si un producto es favorito
SELECT EXISTS(
    SELECT 1 FROM "FavoriteProducts"
    WHERE "UserId" = '550e8400-...' AND "ProductId" = '3fa85f64-...'
);

-- Productos más agregados a favoritos (top 10)
SELECT p."Name", COUNT(fp."Id") AS "FavoriteCount"
FROM "Products" p
INNER JOIN "FavoriteProducts" fp ON p."Id" = fp."ProductId"
GROUP BY p."Id", p."Name"
ORDER BY "FavoriteCount" DESC
LIMIT 10;

-- Usuarios con más favoritos
SELECT u."Email", COUNT(fp."Id") AS "FavoriteCount"
FROM "UserAccounts" u
INNER JOIN "FavoriteProducts" fp ON u."Id" = fp."UserId"
GROUP BY u."Id", u."Email"
ORDER BY "FavoriteCount" DESC;
```

---

## ??? Seguridad

### ? **Aislamiento de Datos**

```csharp
// Usuario solo ve SUS favoritos
var favorites = await db.FavoriteProducts
    .Where(f => f.UserId == userId)  // ? Filtro automático por usuario
    .ToListAsync();
```

### ? **Validaciones**

1. **Producto debe existir:**
```csharp
var product = await db.Products.FindAsync(productId);
if (product == null) throw new InvalidOperationException("Product not found");
```

2. **Producto debe estar activo:**
```csharp
if (!product.IsActive) 
    throw new InvalidOperationException("Cannot add inactive product");
```

3. **JWT validado:**
```csharp
var userId = GetUserIdFromJwt(context);
if (!userId.HasValue) return Results.Unauthorized();
```

### ? **Constraint Único**

El índice único previene duplicados a nivel de base de datos:
```sql
UNIQUE INDEX "UQ_FavoriteProducts_UserId_ProductId"
```

---

## ? **Build Status**

```
? Build successful
? All services registered
? All endpoints mapped
? 0 compilation errors
? 0 warnings
```

---

## ?? **Archivos**

### **Creados (4)**
1. `CC.Domain/Favorites/FavoriteProduct.cs`
2. `CC.Aplication/Favorites/Dtos.cs`
3. `CC.Aplication/Favorites/FavoritesService.cs`
4. `Api-eCommerce/Endpoints/FavoritesEndpoints.cs`

### **Modificados (2)**
1. `CC.Infraestructure/Tenant/TenantDbContext.cs`
2. `Api-eCommerce/Program.cs`

---

## ?? **Próximos Pasos**

### **1. Crear Migración**

```bash
dotnet ef migrations add AddFavoriteProducts \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context TenantDbContext \
  --output-dir Tenant/Migrations
```

### **2. Testing**

```bash
# Test 1: Agregar favorito
curl -X POST "https://api.example.com/me/favorites" \
  -H "X-Tenant-Slug: test-tenant" \
  -H "Authorization: Bearer {token}" \
  -d '{"productId":"..."}'

# Test 2: Listar favoritos
curl -X GET "https://api.example.com/me/favorites" \
  -H "X-Tenant-Slug: test-tenant" \
  -H "Authorization: Bearer {token}"

# Test 3: Verificar favorito
curl -X GET "https://api.example.com/me/favorites/check/{productId}" \
  -H "X-Tenant-Slug: test-tenant" \
  -H "Authorization: Bearer {token}"

# Test 4: Eliminar favorito
curl -X DELETE "https://api.example.com/me/favorites/{productId}" \
  -H "X-Tenant-Slug: test-tenant" \
  -H "Authorization: Bearer {token}"
```

### **3. Mejoras Futuras (Opcionales)**

- [ ] **Notificaciones:** Notificar cuando un favorito baje de precio
- [ ] **Compartir:** Endpoint para compartir lista de favoritos
- [ ] **Listas:** Múltiples listas de favoritos (wishlist, gift registry, etc.)
- [ ] **Estadísticas:** Dashboard de productos más favoritos
- [ ] **Recomendaciones:** Basadas en productos favoritos
- [ ] **Paginación:** Para usuarios con muchos favoritos
- [ ] **Ordenamiento:** Por precio, fecha, nombre, etc.

---

## ?? **Resumen**

| Aspecto | Estado |
|---------|--------|
| **Agregar favoritos** | ? Completado |
| **Listar favoritos** | ? Completado |
| **Eliminar favoritos** | ? Completado |
| **Verificar favorito** | ? Completado |
| **Constraint único** | ? Completado |
| **Idempotencia** | ? Completado |
| **Validaciones** | ? Completado |
| **Build** | ? Exitoso |
| **Documentación** | ? Completada |

---

## ?? **Logros**

? **Sistema completo de favoritos**  
? **Operación idempotente** (no falla al agregar duplicado)  
? **Seguridad robusta** (aislamiento por usuario)  
? **Constraint único** (previene duplicados en DB)  
? **Validaciones completas** (producto existe, está activo)  
? **4 endpoints funcionales**  
? **Documentación completa con ejemplos**  
? **Hook React listo para usar**  

---

**Fecha de implementación:** Diciembre 2024  
**Estado:** ? **PRODUCTION READY** (después de migración)  
**Build:** ? Success  
**Testing:** ? Pendiente
