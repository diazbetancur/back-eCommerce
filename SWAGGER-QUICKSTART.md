# ?? SWAGGER - QUICK START

## ? Inicio Rápido en 3 Pasos

### 1?? Abrir Swagger UI

```
http://localhost:5000/swagger
```

### 2?? Configurar Tenant (Primera Vez)

Abrir la consola del navegador (F12) y ejecutar:

```javascript
SwaggerHelpers.setTenant('acme')
```

La página se recargará y el tenant quedará guardado.

### 3?? Probar un Endpoint

1. Expandir `/api/catalog/products` (GET)
2. Click en **"Try it out"**
3. Click en **"Execute"**
4. Ver la respuesta ?

---

## ?? Endpoints con Autenticación

### Paso 1: Obtener Token

1. Expandir `/auth/login` (POST)
2. Click en **"Try it out"**
3. Body:
   ```json
   {
     "email": "admin@acme.com",
     "password": "password123"
   }
   ```
4. Click en **"Execute"**
5. Copiar el `token` de la respuesta

### Paso 2: Autorizar

1. Click en botón **"Authorize"** ?? (arriba a la derecha)
2. Pegar el token (sin el prefijo 'Bearer ')
3. Click en **"Authorize"**
4. Click en **"Close"**

### Paso 3: Probar Endpoint Autenticado

Ahora puedes probar endpoints que requieren autenticación (ej: `/auth/change-password`)

---

## ?? Endpoints de Carrito/Checkout

Estos endpoints requieren **X-Session-Id** (auto-generado):

### Agregar al Carrito

1. Expandir `/api/cart/add` (POST)
2. **"Try it out"**
3. Body:
   ```json
   {
     "productId": "prod-123",
     "quantity": 2
   }
   ```
4. Verificar headers (auto-completados):
   - ? `X-Tenant-Slug`: `acme`
   - ? `X-Session-Id`: `sess_abc123...`
5. **"Execute"**

### Ver Carrito

1. Expandir `/api/cart` (GET)
2. **"Try it out"**
3. **"Execute"**
4. Ver items en el carrito

### Hacer Checkout

1. Expandir `/api/checkout/place-order` (POST)
2. **"Try it out"**
3. Body:
   ```json
   {
     "shippingAddress": {
       "fullName": "John Doe",
       "phone": "+1234567890",
       "address": "123 Main St",
       "city": "City",
       "country": "US",
       "postalCode": "12345"
     },
     "paymentMethod": "cash"
   }
   ```
4. **"Execute"**

---

## ?? Console Helpers

### Ver Sesión Actual
```javascript
SwaggerHelpers.getCurrentSession()
```

**Respuesta:**
```json
{
  "tenant": "acme",
  "sessionId": "sess_abc123-def456-789..."
}
```

### Cambiar Tenant
```javascript
SwaggerHelpers.setTenant('otro-tenant')
```

### Generar Nueva Session
```javascript
SwaggerHelpers.generateNewSession()
```

### Limpiar Sesión
```javascript
SwaggerHelpers.clearSession()
```

---

## ?? Keyboard Shortcuts

| Shortcut | Acción |
|----------|--------|
| `Ctrl + K` | Focus en búsqueda de endpoints |
| `Ctrl + /` | Mostrar ayuda de shortcuts |
| `Esc` | Cerrar modales |

---

## ?? Documentos Disponibles

Swagger UI permite cambiar entre 3 documentos:

### 1. eCommerce API v1 (por defecto)
- Endpoints de negocio
- Catalog, Cart, Checkout, Features, Auth
- **Requiere**: X-Tenant-Slug

### 2. SuperAdmin API
- Gestión de tenants
- Feature flags
- **No requiere**: X-Tenant-Slug

### 3. Provisioning API
- Registro de nuevos tenants
- Endpoints públicos
- **No requiere**: X-Tenant-Slug

**Cambiar documento**: Dropdown en la parte superior derecha

---

## ?? Búsqueda de Endpoints

1. Click en la barra de filtros (arriba)
2. Escribir: `cart` o `checkout` o `catalog`
3. Solo se mostrarán endpoints que coincidan

---

## ?? Características Visuales

### Métodos HTTP Coloreados

| Método | Color | Uso |
|--------|-------|-----|
| **GET** | ?? Azul | Obtener datos |
| **POST** | ?? Verde | Crear recursos |
| **PUT** | ?? Naranja | Actualizar completo |
| **PATCH** | ?? Morado | Actualizar parcial |
| **DELETE** | ?? Rojo | Eliminar recursos |

### Botones

- **Try it out**: Habilita el formulario de prueba
- **Execute**: Envía la request
- **Clear**: Limpia el formulario
- **Cancel**: Deshabilita el formulario

### Pestañas de Respuesta

- **Response body**: JSON de respuesta
- **Response headers**: Headers HTTP
- **Curl**: Comando curl para copiar

---

## ?? Troubleshooting

### Problema: "X-Tenant-Slug header is required"

**Solución:**
```javascript
SwaggerHelpers.setTenant('acme')
```

### Problema: "Tenant not found"

**Causas:**
- Tenant no existe
- Slug incorrecto

**Verificar:**
```bash
# Listar tenants disponibles (desde SuperAdmin)
curl http://localhost:5000/superadmin/tenants
```

### Problema: 401 Unauthorized

**Causas:**
- Token no proporcionado
- Token expirado

**Solución:**
1. Hacer login en `/auth/login`
2. Copiar nuevo token
3. Click en "Authorize"
4. Pegar token
5. "Authorize"

### Problema: Headers no se auto-completan

**Solución:**
```javascript
// Limpiar y volver a configurar
SwaggerHelpers.clearSession()
SwaggerHelpers.setTenant('acme')
```

### Problema: Session ID no funciona

**Solución:**
```javascript
// Generar nueva session
SwaggerHelpers.generateNewSession()
```

---

## ?? Más Información

- **Documentación completa**: Ver `SWAGGER-DOCUMENTATION.md`
- **Feature Flags**: Ver `FEATURE-FLAGS-README.md`
- **API Examples**: Ver `FEATURE-FLAGS-API-EXAMPLES.md`

---

## ? Checklist de Verificación

Antes de empezar a usar la API:

- [ ] Servidor corriendo en `http://localhost:5000`
- [ ] Swagger UI abre correctamente
- [ ] Tenant configurado (`SwaggerHelpers.getCurrentSession()`)
- [ ] Headers se auto-completan en los endpoints
- [ ] Puedo hacer login y obtener token
- [ ] Token funciona en endpoints autenticados

---

## ?? ¡Listo!

Ya puedes explorar y probar toda la API desde Swagger UI.

**Tip**: Usa los console helpers para agilizar tu workflow.

---

**Última actualización**: Diciembre 2024
