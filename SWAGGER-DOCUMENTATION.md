# ?? Documentación Swagger/OpenAPI - eCommerce Multi-Tenant API

## ?? Implementación Completa

Se ha implementado una documentación Swagger/OpenAPI profesional y completa para la API multi-tenant con las siguientes características:

---

## ? Características Implementadas

### 1. **Múltiples Documentos Swagger**

La documentación está organizada en 3 documentos independientes:

#### ?? **v1 - API Principal**
- **URL**: `/swagger/v1/swagger.json`
- **Contenido**: Endpoints de negocio (Catalog, Cart, Checkout, Features, Auth)
- **Audiencia**: Desarrolladores frontend/mobile

#### ?? **SuperAdmin - API de Administración**
- **URL**: `/swagger/superadmin/swagger.json`
- **Contenido**: Gestión de tenants, feature flags, configuración
- **Audiencia**: Administradores del sistema

#### ?? **Provisioning - API de Registro**
- **URL**: `/swagger/provisioning/swagger.json`
- **Contenido**: Registro y activación de nuevos tenants
- **Audiencia**: Usuarios que desean crear un tenant

---

### 2. **Headers Automáticos**

#### X-Tenant-Slug (Identificación del Tenant)
- **Agregado automáticamente** a todos los endpoints que lo requieren
- **Excluidos**: `/health`, `/provision`, `/superadmin`, `/public`
- **Validación**: Patrón `^[a-z0-9-]{3,50}$`
- **Ejemplos**: `acme`, `demo-store`, `mi-tienda-123`
- **Descripción detallada** con formato, ejemplos y notas

#### X-Session-Id (Sesión de Carrito/Checkout)
- **Agregado automáticamente** a endpoints de `/api/cart` y `/api/checkout`
- **Formato**: UUID o string único (ej: `sess_abc123def456`)
- **Generación**: Auto-generada en el JavaScript personalizado
- **Persistencia**: Guardada en localStorage del navegador

---

### 3. **Respuestas HTTP Documentadas**

Todos los endpoints documentan automáticamente las siguientes respuestas:

#### ? **200/201 - Success**
- Respuesta exitosa específica del endpoint

#### ? **400 - Bad Request**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "X-Tenant-Slug header is required"
}
```
**Causas:**
- Header X-Tenant-Slug no proporcionado
- Header X-Session-Id no proporcionado (carrito/checkout)
- Parámetros inválidos

#### ?? **401 - Unauthorized**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401
}
```
**Causas:**
- Token JWT no proporcionado
- Token JWT expirado o inválido
- Guest checkout deshabilitado

#### ?? **403 - Forbidden**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "Tenant is not active. Current status: Pending"
}
```
**Causas:**
- Tenant no está en estado `Ready`
- Feature flag deshabilitada
- Quota excedida

#### ?? **404 - Not Found**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Tenant 'invalid-slug' not found"
}
```
**Causas:**
- Tenant no existe
- Recurso específico no encontrado

#### ?? **409 - Conflict**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Tenant not resolved or not ready"
}
```
**Causas:**
- Tenant en proceso de provisioning
- Recurso duplicado

#### ?? **422 - Unprocessable Entity**
```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "email": ["The email field is required."],
    "password": ["Password must be at least 8 characters."]
  }
}
```
**Causas:**
- Errores de validación de campos
- Formato inválido

#### ?? **500 - Internal Server Error**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "00-123abc456def789-xyz-00"
}
```
**Causas:**
- Error en la base de datos
- Error en servicio externo
- Error no controlado

---

### 4. **Autenticación JWT Bearer**

Configuración completa de autenticación en Swagger:

#### Botón "Authorize"
- Ubicado en la parte superior derecha
- Permite ingresar el token JWT una vez
- Se incluye automáticamente en todas las requests

#### Formato del Token
```
Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

#### Cómo obtener un token:
1. Hacer login en `/auth/login`
2. Copiar el token de la respuesta
3. Hacer clic en "Authorize" en Swagger
4. Pegar el token (sin el prefijo 'Bearer ')
5. Hacer clic en "Authorize"

---

### 5. **Agrupación de Endpoints**

Los endpoints están organizados por dominio funcional:

| Grupo | Descripción | Endpoints |
|-------|-------------|-----------|
| **Authentication** | Login, cambio de contraseña | `/auth/*` |
| **Catalog** | Productos, categorías, búsqueda | `/api/catalog/*` |
| **Cart** | Agregar/quitar items, obtener carrito | `/api/cart/*` |
| **Checkout** | Cotización, orden de compra | `/api/checkout/*` |
| **Features** | Feature flags del tenant | `/api/features/*` |
| **SuperAdmin** | Gestión de tenants | `/superadmin/*` |
| **Provisioning** | Registro de tenants | `/provision/*` |
| **Public** | Configuración pública | `/public/*` |
| **Health** | Health checks | `/health` |

---

### 6. **UI Personalizada**

#### Estilos CSS Personalizados (`swagger-custom.css`)

**Características:**
- ? Colores corporativos personalizados
- ? Métodos HTTP coloreados (GET=azul, POST=verde, PUT=naranja, DELETE=rojo)
- ? Topbar con branding personalizado
- ? Tags con gradientes
- ? Botones con efectos hover
- ? Scrollbar personalizada
- ? Responsive design
- ? Syntax highlighting para JSON

**Colores:**
```css
--primary-color: #3b82f6 (azul)
--success-color: #10b981 (verde)
--warning-color: #f59e0b (naranja)
--danger-color: #ef4444 (rojo)
--dark-bg: #1f2937 (gris oscuro)
```

#### JavaScript Personalizado (`swagger-custom.js`)

**Funcionalidades:**

1. **Auto-completar headers**
   - Guarda X-Tenant-Slug en localStorage
   - Auto-genera X-Session-Id
   - Rellena automáticamente en cada request

2. **Botón "Clear Session"**
   - Limpia tenant y session guardados
   - Ubicado en el topbar

3. **Información de sesión actual**
   - Muestra tenant y session actuales
   - Actualizado en tiempo real

4. **Notificaciones**
   - Confirmación al copiar curl al portapapeles
   - Alertas visuales con animaciones

5. **Keyboard shortcuts**
   - `Ctrl + K`: Focus en búsqueda
   - `Ctrl + /`: Mostrar ayuda de shortcuts

6. **Console Helpers**
   ```javascript
   SwaggerHelpers.setTenant('acme')
   SwaggerHelpers.setSession('sess_123')
   SwaggerHelpers.generateNewSession()
   SwaggerHelpers.clearSession()
   SwaggerHelpers.getCurrentSession()
   ```

---

## ?? Acceso a Swagger UI

### URL Principal
```
http://localhost:5000/swagger
```

### Documentos Disponibles

1. **eCommerce API v1** (por defecto)
   - Endpoints de negocio
   - Requiere X-Tenant-Slug

2. **SuperAdmin API**
   - Gestión de tenants
   - No requiere X-Tenant-Slug

3. **Provisioning API**
   - Registro público
   - No requiere X-Tenant-Slug

---

## ?? Guía de Uso

### 1. Primera Vez (Configurar Tenant)

1. **Abrir Swagger**: `http://localhost:5000/swagger`
2. **Abrir consola del navegador** (F12)
3. **Establecer tenant**:
   ```javascript
   SwaggerHelpers.setTenant('acme')
   ```
4. **La página se recargará** automáticamente
5. **El tenant estará guardado** para todas las requests futuras

### 2. Probar un Endpoint

#### Ejemplo: GET /api/catalog/products

1. **Expandir el endpoint** (click en la operación)
2. **Click en "Try it out"**
3. **Verificar headers**:
   - `X-Tenant-Slug`: `acme` (auto-completado)
4. **Click en "Execute"**
5. **Ver la respuesta** en tiempo real

#### Ejemplo: POST /api/cart/add (requiere session)

1. **Expandir el endpoint**
2. **Click en "Try it out"**
3. **Verificar headers**:
   - `X-Tenant-Slug`: `acme` (auto-completado)
   - `X-Session-Id`: `sess_abc123...` (auto-generado)
4. **Completar el body**:
   ```json
   {
     "productId": "prod-123",
     "quantity": 2
   }
   ```
5. **Click en "Execute"**

#### Ejemplo: POST /api/checkout/place-order (requiere autenticación)

1. **Obtener token JWT**:
   - Expandir `/auth/login`
   - Try it out
   - Body:
     ```json
     {
       "email": "user@acme.com",
       "password": "password123"
     }
     ```
   - Execute
   - Copiar el token de la respuesta

2. **Autorizar en Swagger**:
   - Click en botón "Authorize" (?? arriba a la derecha)
   - Pegar el token (sin 'Bearer ')
   - Click en "Authorize"

3. **Probar checkout**:
   - Expandir `/api/checkout/place-order`
   - Try it out
   - Headers (auto-completados):
     - `X-Tenant-Slug`: `acme`
     - `X-Session-Id`: `sess_abc123...`
     - `Authorization`: `Bearer {token}`
   - Body:
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
   - Execute

---

## ?? Personalización

### Cambiar Colores

Editar `swagger-custom.css`:

```css
:root {
    --primary-color: #tu-color-primario;
    --success-color: #tu-color-exito;
    --warning-color: #tu-color-advertencia;
    --danger-color: #tu-color-peligro;
}
```

### Agregar Más Helpers JavaScript

Editar `swagger-custom.js`:

```javascript
window.SwaggerHelpers = {
    // Agregar tus funciones personalizadas
    tuFuncionCustom: function() {
        // tu código
    }
};
```

### Agregar Ejemplos a DTOs

Editar `SwaggerExtensions.cs`:

```csharp
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(ProductDto))
        {
            schema.Example = new OpenApiObject
            {
                ["id"] = new OpenApiString("prod-123"),
                ["name"] = new OpenApiString("Product Example"),
                ["price"] = new OpenApiDouble(99.99)
            };
        }
    }
}
```

---

## ?? Archivos Implementados

### Código C#

1. **`SwaggerExtensions.cs`** ?
   - Método `AddMultiTenantSwagger()`: Configura servicios Swagger
   - Método `UseMultiTenantSwaggerUI()`: Configura UI
   - 3 documentos Swagger (v1, superadmin, provisioning)
   - Filtros para enums y ejemplos

2. **`SwaggerTenantOperationFilter.cs`** ? (actualizado)
   - Agrega header X-Tenant-Slug
   - Documentación detallada
   - Validación de formato
   - Múltiples ejemplos

3. **`SwaggerSessionOperationFilter.cs`** ?
   - Agrega header X-Session-Id
   - Solo para endpoints de cart/checkout
   - Descripción de ciclo de vida

4. **`SwaggerCommonResponsesOperationFilter.cs`** ?
   - Documenta respuestas HTTP comunes
   - 400, 401, 403, 404, 409, 422, 500
   - Ejemplos detallados
   - Causas y soluciones

### Assets Frontend

5. **`wwwroot/swagger-custom.css`** ?
   - Estilos personalizados completos
   - Variables CSS reutilizables
   - Responsive design
   - Animaciones suaves

6. **`wwwroot/swagger-custom.js`** ?
   - Auto-completado de headers
   - Persistencia en localStorage
   - Helpers de consola
   - Keyboard shortcuts
   - Notificaciones visuales

### Configuración

7. **`Program.cs`** ? (actualizado)
   - Uso de `AddMultiTenantSwagger()`
   - Uso de `UseMultiTenantSwaggerUI()`
   - Configuración simplificada

---

## ? Checklist de Implementación

- [x] Múltiples documentos Swagger (v1, superadmin, provisioning)
- [x] Header X-Tenant-Slug automático
- [x] Header X-Session-Id automático
- [x] Respuestas HTTP documentadas (400, 401, 403, 404, 409, 422, 500)
- [x] Autenticación JWT Bearer
- [x] Agrupación de endpoints por dominio
- [x] Estilos CSS personalizados
- [x] JavaScript personalizado
- [x] Auto-completado de headers
- [x] Persistencia de sesión
- [x] Console helpers
- [x] Keyboard shortcuts
- [x] Notificaciones visuales
- [x] Ejemplos detallados
- [x] Descripción markdown enriquecida
- [x] Build exitoso

---

## ?? Resultado Final

### Estado: ? **100% COMPLETO**

La documentación Swagger/OpenAPI está completamente implementada y lista para producción con:

- ? **3 documentos** independientes y bien organizados
- ? **Headers automáticos** (X-Tenant-Slug, X-Session-Id)
- ? **7 respuestas HTTP** documentadas con ejemplos
- ? **Autenticación JWT** integrada
- ? **UI personalizada** con branding corporativo
- ? **JavaScript helpers** para productividad
- ? **Auto-completado** de campos comunes
- ? **Persistencia** de sesión en localStorage
- ? **Responsive** para mobile/tablet
- ? **Keyboard shortcuts** para power users

---

## ?? Soporte

### URLs de Documentación
- Swagger UI: `http://localhost:5000/swagger`
- JSON Spec (v1): `http://localhost:5000/swagger/v1/swagger.json`
- JSON Spec (SuperAdmin): `http://localhost:5000/swagger/superadmin/swagger.json`
- JSON Spec (Provisioning): `http://localhost:5000/swagger/provisioning/swagger.json`

### Console Helpers
```javascript
// Ver sesión actual
SwaggerHelpers.getCurrentSession()

// Cambiar tenant
SwaggerHelpers.setTenant('nuevo-tenant')

// Generar nueva session
SwaggerHelpers.generateNewSession()

// Limpiar sesión
SwaggerHelpers.clearSession()
```

### Keyboard Shortcuts
- `Ctrl + K`: Focus en búsqueda
- `Ctrl + /`: Mostrar ayuda

---

**Fecha de implementación:** Diciembre 2024  
**Versión:** 1.0.0  
**Estado:** ? Producción Ready
