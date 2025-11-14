# ??? Architectural Refactoring: Admin vs Tenant Separation

## ?? Overview

Esta refactorización implementa una **separación clara y estricta** entre:
1. **Arquitectura Administrativa** (AdminDb) - Panel de administración global
2. **Arquitectura de Tenants** (TenantDb) - Tiendas individuales

---

## ?? Regla de Oro

> **Los administradores NUNCA usan `X-Tenant-Slug`**
> 
> **Los tenants SIEMPRE usan `X-Tenant-Slug`**

---

## ?? Archivos Creados

### **1. Nuevas Entidades Administrativas**

#### ?? `CC.Infraestructure/Admin/Entities/AdminUser.cs`
```csharp
// Usuario administrador del sistema (NO es usuario de tenant)
public class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string PasswordSalt { get; set; }
    public string FullName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // Navigation
    public ICollection<AdminUserRole> UserRoles { get; set; }
}
```

#### ?? `CC.Infraestructure/Admin/Entities/AdminRole.cs`
```csharp
// Roles: SuperAdmin, TenantManager, Support, Viewer
public class AdminRole
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public ICollection<AdminUserRole> UserRoles { get; set; }
}

public static class AdminRoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantManager = "TenantManager";
    public const string Support = "Support";
    public const string Viewer = "Viewer";
}
```

#### ?? `CC.Infraestructure/Admin/Entities/AdminUserRole.cs`
```csharp
// Relación Many-to-Many
public class AdminUserRole
{
    public Guid AdminUserId { get; set; }
    public Guid AdminRoleId { get; set; }
    public DateTime AssignedAt { get; set; }
    
    // Navigation
    public AdminUser AdminUser { get; set; }
    public AdminRole AdminRole { get; set; }
}
```

---

### **2. DTOs Administrativos**

#### ?? `CC.Aplication/Admin/AdminDtos.cs`
```csharp
// Admin Auth
public record AdminLoginRequest(string Email, string Password);
public record AdminLoginResponse(string Token, DateTime ExpiresAt, AdminUserDto User);
public record AdminUserDto(...);

// Tenant Management
public record TenantListQuery(...);
public record TenantSummaryDto(...);
public record PagedTenantsResponse(...);
public record TenantDetailDto(...);
public record UpdateTenantRequest(...);
public record UpdateTenantStatusRequest(string Status);

// Plan Management
public record PlanDto(...);
public record CreatePlanRequest(...);
public record UpdatePlanRequest(...);

// Admin User Management
public record AdminUserListQuery(...);
public record CreateAdminUserRequest(...);
public record UpdateAdminUserRequest(...);
```

---

### **3. Servicios Administrativos**

#### ?? `CC.Aplication/Admin/AdminAuthService.cs`
```csharp
public interface IAdminAuthService
{
    Task<AdminLoginResponse> LoginAsync(AdminLoginRequest request, CancellationToken ct);
    Task<AdminUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct);
}

public class AdminAuthService : IAdminAuthService
{
    private readonly AdminDbContext _adminDb; // ? SOLO AdminDb
    
    // ? NO usa TenantDbContext
    // ? NO usa ITenantAccessor
    // ? NO requiere X-Tenant-Slug
    
    // Implementa login con AdminUser + AdminRole
    // Genera JWT con claim "admin": "true"
    // Usa PBKDF2 para hashing de contraseñas
}
```

#### ?? `CC.Aplication/Admin/AdminTenantService.cs`
```csharp
public interface IAdminTenantService
{
    Task<PagedTenantsResponse> GetTenantsAsync(TenantListQuery query, CancellationToken ct);
    Task<TenantDetailDto> GetTenantByIdAsync(Guid tenantId, CancellationToken ct);
    Task<TenantDetailDto> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken ct);
    Task<TenantDetailDto> UpdateTenantStatusAsync(Guid tenantId, UpdateTenantStatusRequest request, CancellationToken ct);
    Task DeleteTenantAsync(Guid tenantId, CancellationToken ct);
}

public class AdminTenantService : IAdminTenantService
{
    private readonly AdminDbContext _adminDb; // ? SOLO AdminDb
    
    // ? NO usa TenantDbContext
    // ? Permite listar/filtrar/actualizar tenants
    // ? Incluye info de provisionamiento
}
```

---

### **4. Endpoints Administrativos**

#### ?? `Api-eCommerce/Endpoints/AdminEndpoints.cs`
```csharp
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin")
            .WithTags("Admin Panel")
            .WithOpenApi();

        // ==================== AUTH ====================
        // POST /admin/auth/login
        // GET  /admin/auth/me
        
        // ==================== TENANTS ====================
        // GET    /admin/tenants
        // GET    /admin/tenants/{id}
        // PATCH  /admin/tenants/{id}
        // PATCH  /admin/tenants/{id}/status
        // DELETE /admin/tenants/{id}
        
        return app;
    }
}
```

**Características:**
- ? **NO requieren `X-Tenant-Slug`**
- ? **Solo usan AdminDbContext**
- ? **Autenticación con JWT de AdminUser**
- ? **Claim especial `"admin": "true"` en el token**

---

### **5. Program.cs Refactorizado**

#### ?? `Api-eCommerce/Program.cs`

**Cambios Principales:**

```csharp
// ==================== ADMIN ROUTES (NO TENANT) ====================
// ? NO requieren X-Tenant-Slug
// ? SOLO usan AdminDb
app.MapAdminEndpoints();          // /admin/*
app.MapProvisioningEndpoints();   // /provision/*
app.MapSuperAdminTenants();       // /superadmin/*
app.MapGet("/health", ...);       // /health

// ==================== TENANT-SCOPED ROUTES ====================
// ? REQUIEREN X-Tenant-Slug
// ? Usan TenantDbContext
var tenantGroup = app.MapGroup("")
    .RequireTenantResolution() // ? Middleware personalizado
    .WithOpenApi();

tenantGroup.MapGroup("").MapFeatureFlagsEndpoints();
tenantGroup.MapGroup("").MapTenantAuth();
tenantGroup.MapGroup("").MapCatalogEndpoints();
tenantGroup.MapGroup("").MapCartEndpoints();
tenantGroup.MapGroup("").MapCheckoutEndpoints();
tenantGroup.MapGroup("").MapOrdersEndpoints();
tenantGroup.MapGroup("").MapFavoritesEndpoints();
tenantGroup.MapGroup("").MapLoyaltyEndpoints();
```

**Servicios Registrados:**

```csharp
// Admin Services (NUEVO - SOLO AdminDb)
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IAdminTenantService, AdminTenantService>();

// Business Services (Tenant-Scoped - REQUIEREN X-Tenant-Slug)
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IFavoritesService, FavoritesService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
```

---

### **6. AdminDbContext Actualizado**

#### ?? `CC.Infraestructure/Admin/AdminDbContext.cs`

```csharp
public class AdminDbContext : DbContext
{
    // Tenants y Planes (YA EXISTÍAN)
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<TenantFeatureOverride> TenantFeatureOverrides => Set<TenantFeatureOverride>();
    public DbSet<TenantUsageDaily> TenantUsageDaily => Set<TenantUsageDaily>();
    public DbSet<TenantProvisioning> TenantProvisionings => Set<TenantProvisioning>();

    // Usuarios Administrativos (NUEVO)
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AdminRole> AdminRoles => Set<AdminRole>();
    public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ... configuraciones existentes ...
        
        // ==================== Admin Users & Roles (NUEVO) ====================
        modelBuilder.Entity<AdminUser>(entity => { /* ... */ });
        modelBuilder.Entity<AdminRole>(entity => { /* ... */ });
        modelBuilder.Entity<AdminUserRole>(entity => { /* ... */ });
    }
}
```

---

### **7. TenantResolutionMiddleware Mejorado**

#### ?? `Api-eCommerce/Middleware/TenantResolutionMiddleware.cs`

**Rutas Excluidas (NO requieren tenant):**
```csharp
private static readonly string[] ExcludedPaths = 
{
    "/swagger",
    "/health",
    "/admin",        // ? Panel administrativo
    "/provision",    // ? Provisioning
    "/superadmin",   // ? SuperAdmin
    "/_framework",   // ? Blazor
    "/_vs"           // ? Visual Studio
};
```

**Validaciones Mejoradas:**
- ? Verifica que el tenant esté en status `Ready`
- ? Incluye el `Plan` del tenant en TenantInfo
- ? Logs informativos con emojis (?/?)
- ? Mensajes de error más descriptivos

---

## ?? Flujo de Arquitectura

### **Admin Panel (NO Tenant)**

```
???????????????
? Admin User  ?
???????????????
       ?
       ? POST /admin/auth/login
       ? (Email + Password)
       ?
???????????????????????
? AdminAuthService    ?
? (usa AdminDbContext)?
???????????????????????
       ?
       ? Genera JWT con claim "admin": "true"
       ?
???????????????????????
? Admin Endpoints     ?
? /admin/tenants      ?
? /admin/plans        ?
? /admin/users        ?
???????????????????????
       ?
       ? ? NO requieren X-Tenant-Slug
       ? ? SOLO usan AdminDbContext
       ? ? Pueden ver/editar TODOS los tenants
       ?
???????????????????????
? AdminDbContext      ?
? (Base de datos      ?
?  administrativa)    ?
???????????????????????
```

---

### **Tenant Store (CON Tenant)**

```
???????????????
? Store User  ?
???????????????
       ?
       ? POST /auth/login
       ? Headers: X-Tenant-Slug: mi-tienda
       ?
??????????????????????????
? TenantResolutionMW     ?
? 1. Lee X-Tenant-Slug   ?
? 2. Busca en AdminDb    ?
? 3. Verifica status     ?
? 4. Crea TenantInfo     ?
??????????????????????????
       ?
       ? ? Tenant resolved
       ?
???????????????????????
? AuthService         ?
? (usa TenantDbContext)?
???????????????????????
       ?
       ? Genera JWT con tenant_id + tenant_slug
       ?
???????????????????????
? Tenant Endpoints    ?
? /api/catalog        ?
? /api/cart           ?
? /me/orders          ?
? /me/loyalty         ?
???????????????????????
       ?
       ? ? REQUIEREN X-Tenant-Slug
       ? ? Usan TenantDbContext
       ? ? Solo acceden a SU tenant
       ?
???????????????????????
? TenantDbContext     ?
? (Base de datos del  ?
?  tenant específico) ?
???????????????????????
```

---

## ?? Comparación: Antes vs Después

| Aspecto | ? Antes | ? Después |
|---------|----------|------------|
| **Middleware** | Se ejecutaba en TODAS las rutas | Solo en rutas tenant-scoped |
| **Admin Endpoints** | ? Requerían X-Tenant-Slug | ? NO requieren tenant |
| **Admin Services** | ? No existían | ? AdminAuthService, AdminTenantService |
| **Admin Users** | ? No existían | ? AdminUser, AdminRole, AdminUserRole |
| **Separación** | ? Confusa | ? Clara: Admin vs Tenant |
| **Seguridad** | ? Admins necesitaban tenant | ? Admins independientes |

---

## ?? Endpoints Organizados

### **Admin Panel (NO requieren `X-Tenant-Slug`)**

```
POST   /admin/auth/login                    ? Login admin
GET    /admin/auth/me                       ? Perfil admin

GET    /admin/tenants                       ? Listar tenants
GET    /admin/tenants/{id}                  ? Detalle tenant
PATCH  /admin/tenants/{id}                  ? Actualizar tenant
PATCH  /admin/tenants/{id}/status           ? Cambiar status
DELETE /admin/tenants/{id}                  ? Eliminar tenant

GET    /provision/tenants/{id}/status       ? Status provisioning
POST   /provision/tenants/init              ? Iniciar provisioning
POST   /provision/tenants/confirm           ? Confirmar provisioning

POST   /superadmin/tenants                  ? Crear tenant directo
POST   /superadmin/tenants/repair           ? Reparar tenant

GET    /health                              ? Health check
```

---

### **Tenant Store (REQUIEREN `X-Tenant-Slug`)**

```
Headers: X-Tenant-Slug: mi-tienda

POST   /auth/register                       ? Registro usuario
POST   /auth/login                          ? Login usuario
GET    /auth/me                             ? Perfil usuario

GET    /api/catalog/products                ? Catálogo
POST   /api/cart/items                      ? Agregar al carrito
POST   /api/checkout/place-order            ? Crear orden

GET    /me/orders                           ? Historial órdenes
GET    /me/favorites                        ? Favoritos
GET    /me/loyalty                          ? Puntos loyalty
```

---

## ?? Seguridad

### **Admin JWT Claims**
```json
{
  "sub": "admin-user-id",
  "email": "admin@example.com",
  "admin": "true",              ? Claim especial
  "role": ["SuperAdmin"],
  "jti": "token-id",
  "iat": 1234567890,
  "exp": 1234654290
}
```

### **Tenant JWT Claims**
```json
{
  "sub": "user-id",
  "email": "user@example.com",
  "tenant_id": "tenant-guid",
  "tenant_slug": "mi-tienda",
  "jti": "token-id",
  "iat": 1234567890,
  "exp": 1234654290
}
```

---

## ?? Próximos Pasos

### **1. Migración de AdminDb**
```bash
# Generar migración
dotnet ef migrations add AddAdminUsersAndRoles \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context AdminDbContext \
  --output-dir Admin/Migrations

# Aplicar migración
dotnet ef database update \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --context AdminDbContext
```

### **2. Seed de Datos Administrativos**
```csharp
// Crear SuperAdmin inicial
var (hash, salt) = AdminAuthService.HashPassword("Admin123!");

var superAdmin = new AdminUser
{
    Id = Guid.NewGuid(),
    Email = "admin@yourdomain.com",
    PasswordHash = hash,
    PasswordSalt = salt,
    FullName = "Super Administrator",
    IsActive = true,
    CreatedAt = DateTime.UtcNow
};

var superAdminRole = new AdminRole
{
    Id = Guid.NewGuid(),
    Name = AdminRoleNames.SuperAdmin,
    Description = "Full system access",
    CreatedAt = DateTime.UtcNow
};

var userRole = new AdminUserRole
{
    AdminUserId = superAdmin.Id,
    AdminRoleId = superAdminRole.Id,
    AssignedAt = DateTime.UtcNow
};

adminDb.AdminUsers.Add(superAdmin);
adminDb.AdminRoles.Add(superAdminRole);
adminDb.AdminUserRoles.Add(userRole);
await adminDb.SaveChangesAsync();
```

### **3. Frontend Admin Panel**
El frontend puede consumir estos endpoints SIN necesidad de `X-Tenant-Slug`:

```typescript
// Admin Login
const loginAdmin = async (email: string, password: string) => {
  const response = await fetch('https://api.example.com/admin/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
      // ? NO X-Tenant-Slug
    },
    body: JSON.stringify({ email, password })
  });
  
  const { token, user } = await response.json();
  localStorage.setItem('admin_token', token);
  return user;
};

// List Tenants
const getTenants = async (page = 1) => {
  const response = await fetch(`https://api.example.com/admin/tenants?page=${page}`, {
    headers: {
      'Authorization': `Bearer ${localStorage.getItem('admin_token')}`
      // ? NO X-Tenant-Slug
    }
  });
  
  return await response.json();
};
```

---

## ? Checklist de Implementación

- [x] Crear entidades AdminUser, AdminRole, AdminUserRole
- [x] Actualizar AdminDbContext con nuevos DbSets
- [x] Crear DTOs administrativos
- [x] Crear AdminAuthService
- [x] Crear AdminTenantService
- [x] Crear AdminEndpoints
- [x] Refactorizar Program.cs (separar rutas)
- [x] Actualizar TenantResolutionMiddleware
- [ ] Generar migraciones de AdminDb
- [ ] Aplicar migraciones
- [ ] Seed datos administrativos (SuperAdmin)
- [ ] Crear AdminPlanService (gestión de planes)
- [ ] Crear AdminUserService (CRUD de admins)
- [ ] Tests de endpoints admin
- [ ] Documentar API admin en Swagger

---

## ?? Notas Importantes

### **?? Cambios Breaking**
Si algún endpoint administrativo actualmente **requiere** `X-Tenant-Slug`, debe ser refactorizado para:
1. NO requerir el header
2. Usar solo AdminDbContext
3. Moverse al grupo `/admin`

### **?? Verificación**
Para verificar que la separación está correcta:

```bash
# Endpoints administrativos (sin tenant)
curl -X POST http://localhost:5000/admin/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}'

# Endpoints de tenant (con tenant)
curl -X GET http://localhost:5000/api/catalog/products \
  -H "X-Tenant-Slug: mi-tienda"
```

---

**Última actualización:** Diciembre 2024  
**Versión:** 2.0  
**Estado:** ? **Arquitectura Refactorizada**
