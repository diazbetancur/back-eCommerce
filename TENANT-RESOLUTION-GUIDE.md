# Resolución de Tenants - Guía de Uso

## Descripción General

Sistema de resolución de tenants que permite acceder a bases de datos aisladas por tenant usando un slug identificador.

## Componentes

### 1. TenantInfo (CC.Infraestructure/Tenancy/TenantAccessor.cs)
Información del tenant resuelto en el contexto actual:
```csharp
public class TenantInfo
{
    public Guid Id { get; set; }
    public string Slug { get; set; }
    public string DbName { get; set; }
    public string? Plan { get; set; }
    public string ConnectionString { get; set; }
}
```

### 2. ITenantAccessor (Scoped)
Proporciona acceso thread-safe a la información del tenant en cada request:
```csharp
public interface ITenantAccessor
{
    TenantInfo? TenantInfo { get; }
    void SetTenant(TenantInfo tenantInfo);
    bool HasTenant { get; }
}
```

### 3. TenantResolutionMiddleware
Middleware que:
- Lee `X-Tenant-Slug` header (o query `?tenant=`)
- Consulta Admin DB por slug
- Verifica que status sea "Active"
- Construye connection string desde template
- Guarda TenantInfo en ITenantAccessor

### 4. TenantDbContextFactory
Factory que crea instancias de TenantDbContext:
- `Create()`: Usa tenant del contexto actual (ITenantAccessor)
- `Create(string connectionString)`: Usa connection string específica (para workers)

## Flujo de Resolución

```
1. Request ? Header X-Tenant-Slug: acme
   ?
2. TenantResolutionMiddleware intercepta
   ?
3. Consulta AdminDb.Tenants WHERE Slug = 'acme'
   ?
4. Verifica Status = 'Active'
   ?
5. Lee Tenancy:TenantDbTemplate config
   ?
6. Reemplaza {DbName} con Tenants.DbName
   ?
7. Crea TenantInfo con connection string
   ?
8. Guarda en ITenantAccessor (scoped)
   ?
9. Continúa pipeline ? Controller/Endpoint
   ?
10. Controller usa TenantDbContextFactory.Create()
   ?
11. Factory lee ITenantAccessor.TenantInfo
   ?
12. Crea TenantDbContext con connection string
```

## Configuración en appsettings.json

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

## Uso en Controladores/Endpoints

### Opción 1: Inyectar TenantDbContextFactory
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
        // Crear DbContext del tenant actual
        await using var db = _dbFactory.Create();
        
        var products = await db.Products.ToListAsync();
        return Ok(products);
    }
}
```

### Opción 2: Minimal API con inyección
```csharp
app.MapGet("/api/products", async (TenantDbContextFactory dbFactory) =>
{
    await using var db = dbFactory.Create();
    var products = await db.Products.ToListAsync();
    return Results.Ok(products);
});
```

### Opción 3: Acceder directamente a TenantInfo
```csharp
[HttpGet("tenant-info")]
public IActionResult GetTenantInfo([FromServices] ITenantAccessor tenantAccessor)
{
    if (!tenantAccessor.HasTenant)
    {
        return BadRequest("No tenant resolved");
    }
    
    var info = tenantAccessor.TenantInfo;
    return Ok(new
    {
        slug = info.Slug,
        plan = info.Plan,
        dbName = info.DbName // No exponer connection string
    });
}
```

## Ejemplos cURL

### 1. Request con Header (Recomendado)
```bash
curl -X GET "http://localhost:5000/api/products" \
  -H "X-Tenant-Slug: acme" \
  -H "Content-Type: application/json"
```

### 2. Request con Query String (Alternativa)
```bash
curl -X GET "http://localhost:5000/api/products?tenant=acme" \
  -H "Content-Type: application/json"
```

### 3. Request sin Tenant (Error 400)
```bash
curl -X GET "http://localhost:5000/api/products" \
  -H "Content-Type: application/json"

# Response:
{
  "error": "Tenant slug is required",
  "detail": "Provide tenant slug via X-Tenant-Slug header or ?tenant query parameter"
}
```

### 4. Tenant No Existe (Error 404)
```bash
curl -X GET "http://localhost:5000/api/products" \
  -H "X-Tenant-Slug: noexiste"

# Response:
{
  "error": "Tenant not found",
  "detail": "No tenant found with slug 'noexiste'"
}
```

### 5. Tenant No Activo (Error 403)
```bash
curl -X GET "http://localhost:5000/api/products" \
  -H "X-Tenant-Slug: pending-tenant"

# Response:
{
  "error": "Tenant not available",
  "detail": "Tenant 'pending-tenant' is in 'Provisioning' status",
  "status": "Provisioning"
}
```

## Rutas Excluidas (No Requieren Tenant)

Las siguientes rutas NO requieren el header X-Tenant-Slug:

- `/swagger/*` - Documentación Swagger
- `/health` - Health checks
- `/provision/tenants/init` - Iniciar aprovisionamiento
- `/provision/tenants/confirm` - Confirmar aprovisionamiento
- `/provision/tenants/{id}/status` - Estado de aprovisionamiento
- `/superadmin/*` - Endpoints de super administrador

## Swagger UI

El Swagger UI automáticamente documenta el header X-Tenant-Slug en todos los endpoints que lo requieren.

Acceder a: `http://localhost:5000/swagger`

En Swagger verás:
- Header `X-Tenant-Slug` marcado como requerido
- Query parameter `tenant` como alternativa
- Respuestas 400, 403, 404 documentadas

## Estados del Tenant

El tenant debe estar en estado "Active" para poder ser usado:

| Estado | Descripción | Permite Acceso |
|--------|-------------|----------------|
| PENDING_VALIDATION | Esperando confirmación | ? No |
| QUEUED | En cola para aprovisionamiento | ? No |
| Provisioning | Siendo aprovisionado | ? No |
| Active | Activo y listo para usar | ? Sí |
| Failed | Aprovisionamiento falló | ? No |
| Suspended | Suspendido por admin | ? No |

## Uso en Background Workers

Para usar en workers o servicios que no tienen HttpContext:

```csharp
public class MyBackgroundService : BackgroundService
{
    private readonly TenantDbContextFactory _dbFactory;
    private readonly IConfiguration _configuration;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Construir connection string manualmente
        var template = _configuration["Tenancy:TenantDbTemplate"];
        var connectionString = template.Replace("{DbName}", "ecom_tenant_acme");
        
        // Crear DbContext con connection string específica
        await using var db = _dbFactory.Create(connectionString);
        
        // Usar el DbContext...
        var products = await db.Products.ToListAsync(stoppingToken);
    }
}
```

## Logging

El middleware registra eventos estructurados:

```
INFO: Tenant resolved successfully. Slug: acme, DbName: ecom_tenant_acme, Plan: Premium
WARN: Tenant slug not provided. Path: /api/products
WARN: Tenant not found. Slug: noexiste
WARN: Tenant not active. Slug: pending, Status: Provisioning
ERROR: Error resolving tenant: {Exception}
```

## Seguridad

### ? Implementado
- Connection string nunca se expone en respuestas
- Solo tenants "Active" pueden ser accedidos
- Validación de slug en Admin DB
- Tenant aislado por request (scoped)

### ?? Consideraciones
- Validar permisos adicionales en controladores si es necesario
- No confiar solo en el slug para autorización
- Implementar rate limiting por tenant
- Monitorear uso de cada tenant

## Troubleshooting

### Error: "Cannot create TenantDbContext: No tenant has been resolved"
**Causa**: El middleware TenantResolutionMiddleware no se ejecutó o falló.

**Solución**:
1. Verificar que el header X-Tenant-Slug o query ?tenant esté presente
2. Verificar orden de middlewares en Program.cs
3. Verificar que la ruta no esté en la lista de excluidas

### Error: "Tenancy:TenantDbTemplate configuration is missing"
**Causa**: Configuración faltante en appsettings.json.

**Solución**:
```json
{
  "Tenancy": {
    "TenantDbTemplate": "Host=localhost;Database={DbName};Username=postgres;Password=postgres;"
  }
}
```

### Tenant resuelto pero error de conexión
**Causa**: Connection string mal formado o BD del tenant no existe.

**Solución**:
1. Verificar que el tenant haya sido aprovisionado correctamente
2. Verificar que la BD existe: `SELECT datname FROM pg_database WHERE datname = 'ecom_tenant_acme';`
3. Verificar plantilla TenantDbTemplate en config

## Testing

### Unit Tests
```csharp
[Test]
public void TenantAccessor_SetTenant_StoresCorrectly()
{
    var accessor = new TenantAccessor();
    var info = new TenantInfo { Slug = "test", DbName = "ecom_tenant_test" };
    
    accessor.SetTenant(info);
    
    Assert.IsTrue(accessor.HasTenant);
    Assert.AreEqual("test", accessor.TenantInfo.Slug);
}
```

### Integration Tests
```csharp
[Test]
public async Task TenantResolution_ValidSlug_ResolvesCorrectly()
{
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Tenant-Slug", "acme");
    
    var response = await client.GetAsync("/api/products");
    
    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
}
```

## Migración desde Sistema Anterior

Si estabas usando ITenantResolver/TenantContext:

**Antes:**
```csharp
var tenant = await _tenantResolver.ResolveAsync(context);
var connectionString = tenant.ConnectionString;
```

**Ahora:**
```csharp
var tenantInfo = _tenantAccessor.TenantInfo;
var connectionString = tenantInfo.ConnectionString;
```

O mejor aún, usar el factory:
```csharp
await using var db = _dbFactory.Create();
// Ya tiene la conexión correcta
```

---

**Autor**: Sistema de IA
**Fecha**: 2025-01-10
**Versión**: 1.0
