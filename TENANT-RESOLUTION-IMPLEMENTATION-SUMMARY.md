# Sistema de Resolución de Tenants - Resumen de Implementación

## ? Archivos Creados

### 1. Tenancy Infrastructure (CC.Infraestructure/Tenancy/)
- **TenantAccessor.cs**: 
  - `TenantInfo`: Clase con información del tenant (Id, Slug, DbName, Plan, ConnectionString)
  - `ITenantAccessor`: Interfaz para acceso thread-safe al tenant actual
  - `TenantAccessor`: Implementación scoped que guarda el tenant por request

### 2. Middleware Actualizado (Api-eCommerce/Middleware/)
- **TenantResolutionMiddleware.cs**: 
  - Lee X-Tenant-Slug header (fallback query ?tenant)
  - Consulta AdminDb.Tenants por Slug
  - Verifica Status = "Active"
  - Construye connection string desde TenantDbTemplate
  - Guarda TenantInfo en ITenantAccessor
  - Responde 400/404/403 según corresponda
  - Rutas excluidas: /swagger, /health, /provision, /superadmin

### 3. Factory Actualizado (CC.Infraestructure/Tenant/)
- **TenantDbContextFactory.cs**:
  - `Create()`: Usa tenant del ITenantAccessor (contexto actual)
  - `Create(string cs)`: Usa connection string específica (para workers)
  - Valida que haya tenant resuelto antes de crear DbContext
  - Error descriptivo si no hay tenant

### 4. Swagger Extension (Api-eCommerce/Extensions/)
- **SwaggerTenantOperationFilter.cs**:
  - Agrega header X-Tenant-Slug a todos los endpoints (excepto excluidos)
  - Agrega query parameter "tenant" como alternativa
  - Documenta respuestas 400, 403, 404

### 5. Metering Actualizado (Api-eCommerce/Metering/)
- **MeteringMiddleware.cs**:
  - Actualizado para usar ITenantAccessor en lugar de TenantContext
  - TODO: Implementar lógica de metering cuando TenantUsageDaily esté disponible

### 6. Program.cs Actualizado
- Registro de ITenantAccessor como Scoped
- Registro de TenantDbContextFactory como Scoped
- SwaggerGen con SwaggerTenantOperationFilter
- Orden correcto de middlewares

### 7. Documentación
- **TENANT-RESOLUTION-GUIDE.md**: Guía completa con ejemplos cURL y casos de uso

## ?? Características Implementadas

### Resolución de Tenant
? Lee X-Tenant-Slug header (prioridad)
? Fallback a query parameter ?tenant=
? Consulta Admin DB por slug
? Verifica Status = "Active"
? Construye connection string desde template
? Guarda en contexto scoped (ITenantAccessor)

### Validaciones
? 400 Bad Request si no se proporciona slug
? 404 Not Found si tenant no existe
? 403 Forbidden si tenant no está Active
? 500 Internal Server Error en caso de error inesperado

### Rutas Excluidas (No Requieren Tenant)
? /swagger/* - Documentación
? /health - Health checks
? /provision/tenants/* - Aprovisionamiento
? /superadmin/* - Admin endpoints

### Factory Pattern
? `Create()` usa tenant del contexto actual
? `Create(string cs)` permite especificar connection string
? Validación de tenant antes de crear DbContext
? Errores descriptivos

### Swagger Integration
? Header X-Tenant-Slug documentado automáticamente
? Query parameter como alternativa
? Respuestas comunes documentadas
? Ejemplo de valor en cada parámetro

## ?? Flujo de Resolución

```
Request con X-Tenant-Slug: acme
    ?
TenantResolutionMiddleware
    ?
Consulta AdminDb.Tenants WHERE Slug = 'acme'
    ?
Verifica Status = 'Active'
    ?
Lee Tenancy:TenantDbTemplate config
    ?
Reemplaza {DbName} con tenant.DbName
    ?
Crea TenantInfo con connection string
    ?
Guarda en ITenantAccessor (scoped)
    ?
Pipeline continúa ? Controller
    ?
Controller usa TenantDbContextFactory.Create()
    ?
Factory lee ITenantAccessor.TenantInfo
    ?
Crea TenantDbContext con connection string correcta
```

## ?? Configuración Requerida

### appsettings.json
```json
{
  "ConnectionStrings": {
    "AdminDb": "Host=localhost;Database=ecommerce_admin;Username=postgres;Password=postgres;"
  },
  "Tenancy": {
    "TenantDbTemplate": "Host=localhost;Database={DbName};Username=postgres;Password=postgres;"
  }
}
```

### Program.cs (Ya configurado)
```csharp
// Registro de servicios
builder.Services.AddScoped<ITenantAccessor, TenantAccessor>();
builder.Services.AddScoped<TenantDbContextFactory>();

// Middleware (orden importante)
app.UseMiddleware<MeteringMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
```

## ?? Ejemplos de Uso

### En Controladores
```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly TenantDbContextFactory _dbFactory;
    
    public ProductsController(TenantDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        // Automáticamente usa el tenant resuelto
        await using var db = _dbFactory.Create();
        var products = await db.Products.ToListAsync();
        return Ok(products);
    }
}
```

### En Minimal APIs
```csharp
app.MapGet("/api/products", async (TenantDbContextFactory dbFactory) =>
{
    await using var db = dbFactory.Create();
    var products = await db.Products.ToListAsync();
    return Results.Ok(products);
});
```

### Acceder a Información del Tenant
```csharp
[HttpGet("info")]
public IActionResult GetInfo([FromServices] ITenantAccessor tenantAccessor)
{
    if (!tenantAccessor.HasTenant)
        return BadRequest("No tenant");
    
    var info = tenantAccessor.TenantInfo;
    return Ok(new
    {
        slug = info.Slug,
        plan = info.Plan,
        dbName = info.DbName
        // NO exponer connectionString
    });
}
```

## ?? Ejemplos cURL

### Request Normal (con Header)
```bash
curl -X GET "http://localhost:5000/api/products" \
  -H "X-Tenant-Slug: acme"
```

### Request con Query String
```bash
curl -X GET "http://localhost:5000/api/products?tenant=acme"
```

### Request sin Tenant (400)
```bash
curl -X GET "http://localhost:5000/api/products"

# Response:
{
  "error": "Tenant slug is required",
  "detail": "Provide tenant slug via X-Tenant-Slug header or ?tenant query parameter"
}
```

### Tenant No Existe (404)
```bash
curl -X GET "http://localhost:5000/api/products" \
  -H "X-Tenant-Slug: noexiste"

# Response:
{
  "error": "Tenant not found",
  "detail": "No tenant found with slug 'noexiste'"
}
```

### Tenant No Activo (403)
```bash
curl -X GET "http://localhost:5000/api/products" \
  -H "X-Tenant-Slug: pending"

# Response:
{
  "error": "Tenant not available",
  "detail": "Tenant 'pending' is in 'Provisioning' status",
  "status": "Provisioning"
}
```

## ?? Orden de Middlewares (Crítico)

```csharp
// 1. HTTPS Redirection
app.UseHttpsRedirection();

// 2. Metering (primero para capturar todo)
app.UseMiddleware<MeteringMiddleware>();

// 3. Tenant Resolution (antes de auth)
app.UseMiddleware<TenantResolutionMiddleware>();

// 4. Error Handling
app.UseMiddleware(typeof(ErrorHandlingMiddleware));

// 5. Authentication & Authorization
app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();

// 6. Controllers y Endpoints
app.MapControllers();
```

## ?? Seguridad

### ? Implementado
- Connection string nunca se expone
- Tenant aislado por request (scoped)
- Solo tenants "Active" accesibles
- Validación en Admin DB
- Logging de eventos

### ?? Logging

```
INFO: Tenant resolved successfully. Slug: acme, DbName: ecom_tenant_acme, Plan: Premium
WARN: Tenant slug not provided. Path: /api/products
WARN: Tenant not found. Slug: noexiste
WARN: Tenant not active. Slug: pending, Status: Provisioning
ERROR: Error resolving tenant: {Exception}
DEBUG: Creating TenantDbContext for tenant acme (DbName: ecom_tenant_acme)
```

## ?? Uso en Background Workers

```csharp
public class MyWorker : BackgroundService
{
    private readonly TenantDbContextFactory _dbFactory;
    private readonly IConfiguration _config;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Construir connection string manualmente
        var template = _config["Tenancy:TenantDbTemplate"];
        var cs = template.Replace("{DbName}", "ecom_tenant_acme");
        
        // Usar Create(string) para workers
        await using var db = _dbFactory.Create(cs);
        var products = await db.Products.ToListAsync(ct);
    }
}
```

## ?? Swagger UI

Accede a: `http://localhost:5000/swagger`

Verás:
- Header **X-Tenant-Slug** (required) en endpoints
- Query parameter **tenant** como alternativa
- Respuestas 400, 403, 404 documentadas
- Ejemplo: "acme" en cada parámetro

## ?? Troubleshooting

### "Cannot create TenantDbContext: No tenant has been resolved"
**Solución**:
1. Verificar header X-Tenant-Slug o query ?tenant
2. Verificar orden de middlewares
3. Verificar que la ruta no esté excluida

### "Tenancy:TenantDbTemplate configuration is missing"
**Solución**: Agregar en appsettings.json:
```json
{
  "Tenancy": {
    "TenantDbTemplate": "Host=...;Database={DbName};..."
  }
}
```

### Tenant resuelto pero error de conexión
**Solución**:
1. Verificar que tenant esté aprovisionado
2. Verificar que BD existe
3. Verificar template en config

## ? Validación

- ? Compila correctamente
- ? ITenantAccessor registrado como Scoped
- ? TenantDbContextFactory registrado
- ? Middleware configurado
- ? Swagger filter aplicado
- ? Logging implementado
- ? Documentación completa

## ?? Servicios Registrados

```csharp
// Scoped (por request)
builder.Services.AddScoped<ITenantAccessor, TenantAccessor>();
builder.Services.AddScoped<TenantDbContextFactory>();

// DbContext Admin
builder.Services.AddDbContext<AdminDbContext>(...);
```

## ?? Migración desde Sistema Anterior

**Antes (ITenantResolver/TenantContext):**
```csharp
var tenant = await _resolver.ResolveAsync(context);
var cs = tenant.ConnectionString;
```

**Ahora (ITenantAccessor/TenantInfo):**
```csharp
var info = _tenantAccessor.TenantInfo;
var cs = info.ConnectionString;
```

**Mejor (usar Factory):**
```csharp
await using var db = _dbFactory.Create();
```

## ?? Estados de Tenant

| Estado | Permite Acceso |
|--------|----------------|
| PENDING_VALIDATION | ? No |
| QUEUED | ? No |
| Provisioning | ? No |
| **Active** | ? **Sí** |
| Failed | ? No |
| Suspended | ? No |

## ?? Próximos Pasos

1. ? **Completado**: Sistema de resolución
2. ? **Completado**: Factory pattern
3. ? **Completado**: Swagger integration
4. ? **Pendiente**: Implementar metering real
5. ? **Pendiente**: Tests de integración
6. ? **Pendiente**: Rate limiting por tenant
7. ? **Pendiente**: Caching de tenant info

---

**Estado**: ? Implementación completa y funcional
**Build**: ? Exitoso
**Documentación**: ? Completa
**Ejemplos**: ? Incluidos
**Autor**: Sistema de IA
**Fecha**: 2025-01-10
**Versión**: 1.0
