# ?? Swagger/OpenAPI - eCommerce Multi-Tenant API

## ?? Estado: ? IMPLEMENTACIÓN COMPLETA

> Documentación Swagger/OpenAPI profesional con UI personalizada, headers automáticos, múltiples documentos y experiencia de usuario optimizada.

---

## ?? Tabla de Contenidos

- [?? Quick Start](#-quick-start)
- [?? Documentos Swagger](#-documentos-swagger)
- [?? Headers Automáticos](#-headers-automáticos)
- [?? Console Helpers](#-console-helpers)
- [?? Keyboard Shortcuts](#?-keyboard-shortcuts)
- [?? UI Personalizada](#-ui-personalizada)
- [?? Archivos Implementados](#-archivos-implementados)
- [?? Personalización](#-personalización)
- [?? Troubleshooting](#-troubleshooting)

---

## ?? Quick Start

### 1. Abrir Swagger UI
```
http://localhost:5000/swagger
```

### 2. Configurar Tenant (Primera Vez)
Abrir consola del navegador (F12):
```javascript
SwaggerHelpers.setTenant('acme')
```

### 3. Probar un Endpoint
1. Expandir `/api/catalog/products`
2. Click en **"Try it out"**
3. Click en **"Execute"**
4. ? Ver la respuesta

**[Ver guía completa ?](SWAGGER-QUICKSTART.md)**

---

## ?? Documentos Swagger

La API está documentada en **3 documentos independientes**:

### ?? **eCommerce API v1** (Principal)
```
/swagger/v1/swagger.json
```
**Contenido:**
- ?? Catalog (productos, categorías)
- ??? Cart (agregar/quitar items)
- ?? Checkout (cotización, órdenes)
- ?? Features (feature flags)
- ?? Authentication (login, JWT)

**Requiere:** `X-Tenant-Slug` header

---

### ?? **SuperAdmin API**
```
/swagger/superadmin/swagger.json
```
**Contenido:**
- ?? Gestión de tenants
- ?? Feature flags por tenant
- ?? Configuración global

**No requiere:** `X-Tenant-Slug`

---

### ?? **Provisioning API**
```
/swagger/provisioning/swagger.json
```
**Contenido:**
- ?? Registro de nuevos tenants
- ?? Confirmación por email
- ?? Activación automática

**Público:** No requiere autenticación

---

## ?? Headers Automáticos

### X-Tenant-Slug
**Agregado automáticamente** a todos los endpoints que lo requieren.

**Formato:** `^[a-z0-9-]{3,50}$`

**Ejemplos válidos:**
```
? acme
? demo-store
? mi-tienda-123
? company-xyz
```

**Ejemplos inválidos:**
```
? ACME (mayúsculas)
? my_store (guion bajo)
? ab (muy corto)
? my store (espacios)
```

**Persistencia:** Se guarda en localStorage automáticamente

---

### X-Session-Id
**Agregado automáticamente** en endpoints de `/api/cart` y `/api/checkout`.

**Formato:** UUID o string único

**Ejemplo:**
```
sess_abc123def456
```

**Auto-generación:** Se genera automáticamente la primera vez

**Persistencia:** Se guarda en localStorage

---

## ?? Console Helpers

Funciones útiles disponibles en la consola del navegador:

### Ver Sesión Actual
```javascript
SwaggerHelpers.getCurrentSession()
```
**Retorna:**
```json
{
  "tenant": "acme",
  "sessionId": "sess_abc123-def456..."
}
```

### Cambiar Tenant
```javascript
SwaggerHelpers.setTenant('nuevo-tenant')
```

### Generar Nueva Session
```javascript
SwaggerHelpers.generateNewSession()
```

### Limpiar Sesión
```javascript
SwaggerHelpers.clearSession()
```

### Establecer Session ID Personalizado
```javascript
SwaggerHelpers.setSession('mi-session-custom')
```

---

## ?? Keyboard Shortcuts

| Shortcut | Acción |
|----------|--------|
| **`Ctrl + K`** | Focus en búsqueda de endpoints |
| **`Ctrl + /`** | Mostrar ayuda de shortcuts |
| **`Esc`** | Cerrar modales abiertos |

---

## ?? UI Personalizada

### Métodos HTTP Coloreados

| Método | Color | Uso |
|--------|-------|-----|
| **GET** | ?? Azul (#3b82f6) | Obtener datos |
| **POST** | ?? Verde (#10b981) | Crear recursos |
| **PUT** | ?? Naranja (#f59e0b) | Actualizar completo |
| **PATCH** | ?? Morado (#8b5cf6) | Actualizar parcial |
| **DELETE** | ?? Rojo (#ef4444) | Eliminar recursos |

### Características Visuales

- ? Topbar personalizado con branding
- ? Tags con gradientes
- ? Botones con efectos hover
- ? Scrollbar personalizada
- ? Dark mode en ejemplos de código
- ? Animaciones suaves
- ? Responsive design (mobile-friendly)
- ? Syntax highlighting para JSON

### Botones Especiales

- **??? Clear Session**: Limpia tenant y session guardados (en topbar)
- **?? Session Info**: Muestra tenant y session actuales (en topbar)
- **?? Authorize**: Ingresar token JWT para endpoints autenticados

---

## ?? Archivos Implementados

### Código C# (4 archivos)

| Archivo | Estado | Líneas | Descripción |
|---------|--------|--------|-------------|
| `SwaggerExtensions.cs` | ? NUEVO | ~400 | Configuración multi-tenant |
| `SwaggerTenantOperationFilter.cs` | ? ACTUALIZADO | ~150 | Header X-Tenant-Slug |
| `SwaggerSessionOperationFilter.cs` | ? NUEVO | ~100 | Header X-Session-Id |
| ~~`SwaggerCommonResponsesOperationFilter.cs`~~ | ? INCLUIDO EN `SwaggerSessionOperationFilter.cs` | ~500 | Respuestas HTTP comunes |

### Assets Frontend (2 archivos)

| Archivo | Estado | Líneas | Descripción |
|---------|--------|--------|-------------|
| `swagger-custom.css` | ? NUEVO | ~400 | Estilos personalizados |
| `swagger-custom.js` | ? NUEVO | ~350 | JavaScript helpers |

### Configuración (1 archivo)

| Archivo | Estado | Cambios | Descripción |
|---------|--------|---------|-------------|
| `Program.cs` | ? ACTUALIZADO | 10 líneas | Integración de extensiones |

### Documentación (3 archivos)

| Archivo | Páginas | Contenido |
|---------|---------|-----------|
| `SWAGGER-DOCUMENTATION.md` | ~15 | Documentación técnica completa |
| `SWAGGER-QUICKSTART.md` | ~6 | Guía de inicio rápido |
| `SWAGGER-FINAL-DELIVERY.md` | ~10 | Resumen ejecutivo |

---

## ?? Personalización

### Cambiar Colores Corporativos

Editar `Api-eCommerce/wwwroot/swagger-custom.css`:

```css
:root {
    --primary-color: #tu-color-primario;
    --success-color: #tu-color-exito;
    --warning-color: #tu-color-advertencia;
    --danger-color: #tu-color-peligro;
}
```

### Agregar Console Helpers Personalizados

Editar `Api-eCommerce/wwwroot/swagger-custom.js`:

```javascript
window.SwaggerHelpers.tuFuncion = function() {
    // tu lógica personalizada
};
```

### Agregar Ejemplos a DTOs

Editar `Api-eCommerce/Extensions/SwaggerExtensions.cs`:

```csharp
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(TuDto))
        {
            schema.Example = new OpenApiObject
            {
                ["propiedad"] = new OpenApiString("valor ejemplo")
            };
        }
    }
}
```

---

## ?? Troubleshooting

### ? Swagger UI no carga

**Síntoma:** Página en blanco o error 404

**Soluciones:**
1. Verificar que el servidor esté corriendo:
   ```bash
   curl http://localhost:5000/health
   ```
2. Verificar que estés usando la URL correcta:
   ```
   http://localhost:5000/swagger
   ```
3. Verificar logs de la consola del navegador (F12)

---

### ? Headers no se auto-completan

**Síntoma:** X-Tenant-Slug y X-Session-Id están vacíos

**Soluciones:**
1. Configurar tenant manualmente:
   ```javascript
   SwaggerHelpers.setTenant('acme')
   ```
2. Verificar que JavaScript se haya cargado:
   ```javascript
   console.log(SwaggerHelpers)
   ```
3. Limpiar y reconfigurar:
   ```javascript
   SwaggerHelpers.clearSession()
   SwaggerHelpers.setTenant('acme')
   ```

---

### ? 401 Unauthorized en endpoints autenticados

**Síntoma:** Error 401 al probar endpoints protegidos

**Soluciones:**
1. Obtener token válido:
   - Expandir `/auth/login`
   - Try it out
   - Execute
   - Copiar token de la respuesta

2. Autorizar en Swagger:
   - Click en botón **"Authorize"** ??
   - Pegar token (sin 'Bearer ')
   - Click en **"Authorize"**

3. Verificar que el token no haya expirado:
   - Los tokens tienen duración de 60 minutos
   - Obtener nuevo token si expiró

---

### ? CSS/JavaScript no se aplican

**Síntoma:** UI estándar de Swagger sin personalización

**Soluciones:**
1. Verificar que los archivos existan:
   ```bash
   ls Api-eCommerce/wwwroot/swagger-custom.css
   ls Api-eCommerce/wwwroot/swagger-custom.js
   ```

2. Verificar que se estén sirviendo correctamente:
   ```bash
   curl http://localhost:5000/swagger-custom.css
   curl http://localhost:5000/swagger-custom.js
   ```

3. Limpiar caché del navegador:
   - `Ctrl + Shift + R` (Windows/Linux)
   - `Cmd + Shift + R` (Mac)

---

### ? Tenant no existe

**Síntoma:** Error 404 "Tenant not found"

**Soluciones:**
1. Verificar que el tenant exista:
   ```bash
   curl http://localhost:5000/superadmin/tenants
   ```

2. Crear el tenant si no existe:
   ```bash
   curl -X POST http://localhost:5000/provision/tenants/init \
     -H "Content-Type: application/json" \
     -d '{"slug": "acme", ...}'
   ```

3. Verificar el slug:
   ```javascript
   SwaggerHelpers.getCurrentSession()
   ```

---

## ?? Estadísticas

### Código
- **Total archivos**: 10
- **Líneas de código**: ~1,900
- **Líneas CSS**: ~400
- **Líneas JavaScript**: ~350
- **Build status**: ? Exitoso

### Documentación
- **Total documentos**: 3
- **Páginas**: ~30
- **Palabras**: ~5,000
- **Ejemplos**: 25+

### Features
- **Documentos Swagger**: 3
- **Headers automáticos**: 2
- **Respuestas HTTP**: 8
- **Console helpers**: 5
- **Keyboard shortcuts**: 3
- **Colores personalizados**: 5

---

## ?? Documentación Relacionada

### Swagger
- ?? **[SWAGGER-QUICKSTART.md](SWAGGER-QUICKSTART.md)** - Inicio rápido en 3 pasos
- ?? **[SWAGGER-DOCUMENTATION.md](SWAGGER-DOCUMENTATION.md)** - Documentación completa
- ? **[SWAGGER-FINAL-DELIVERY.md](SWAGGER-FINAL-DELIVERY.md)** - Resumen ejecutivo

### Feature Flags
- ?? **[FEATURE-FLAGS-README.md](FEATURE-FLAGS-README.md)** - Índice completo
- ?? **[FEATURE-FLAGS-TESTING-GUIDE.md](FEATURE-FLAGS-TESTING-GUIDE.md)** - Guía de testing
- ?? **[FEATURE-FLAGS-API-EXAMPLES.md](FEATURE-FLAGS-API-EXAMPLES.md)** - Ejemplos curl

---

## ? Checklist de Verificación

### Antes de empezar:
- [ ] Servidor corriendo en `http://localhost:5000`
- [ ] Swagger UI abre correctamente
- [ ] JavaScript se cargó (verificar consola)
- [ ] CSS se aplicó (verificar colores)

### Primera configuración:
- [ ] Tenant configurado con `SwaggerHelpers.setTenant()`
- [ ] Headers se auto-completan en los endpoints
- [ ] Información de sesión visible en topbar
- [ ] Botón "Clear Session" visible

### Testing básico:
- [ ] Puedo expandir endpoints
- [ ] Puedo hacer "Try it out"
- [ ] Headers aparecen con valores
- [ ] Puedo hacer "Execute"
- [ ] Veo respuestas correctamente

### Testing con autenticación:
- [ ] Puedo hacer login en `/auth/login`
- [ ] Obtengo token JWT válido
- [ ] Botón "Authorize" funciona
- [ ] Token se incluye en requests autenticadas
- [ ] Endpoints protegidos responden 200 (no 401)

---

## ?? ¡Todo Listo!

Tu documentación Swagger/OpenAPI está **100% completa** y lista para usar.

### URLs Rápidas:
- ?? **Swagger UI**: http://localhost:5000/swagger
- ?? **Spec JSON (v1)**: http://localhost:5000/swagger/v1/swagger.json
- ?? **Spec JSON (SuperAdmin)**: http://localhost:5000/swagger/superadmin/swagger.json
- ?? **Spec JSON (Provisioning)**: http://localhost:5000/swagger/provisioning/swagger.json

### Próximos Pasos:
1. ? Explorar los 3 documentos Swagger
2. ? Configurar tu tenant con `SwaggerHelpers.setTenant()`
3. ? Probar endpoints de ejemplo
4. ? Obtener token JWT y probar endpoints autenticados
5. ? Usar console helpers para agilizar tu workflow

---

**Última actualización**: Diciembre 2024  
**Versión**: 1.0.0  
**Estado**: ? **PRODUCCIÓN READY**  
**Build**: ? **EXITOSO**

---

Made with ?? for eCommerce Multi-Tenant API
