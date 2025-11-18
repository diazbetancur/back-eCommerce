# ?? Seeders - Guía Completa

## ?? Resumen

Este documento explica los **dos seeders automáticos** implementados en el sistema:

1. **AdminDbSeeder** - Crea el usuario SuperAdmin global del sistema
2. **TenantDbSeeder** - Crea el usuario Admin de cada tenant individual

---

## ??? Arquitectura de Usuarios

```
???????????????????????????????????????????????????????????
?                  ADMIN DB (Global)                      ?
???????????????????????????????????????????????????????????
?                                                          ?
?  ?? SuperAdmin (admin@yourdomain.com)                   ?
?     ?? Gestiona TODOS los tenants                       ?
?     ?? Panel: /admin                                    ?
?     ?? NO necesita X-Tenant-Slug                        ?
?                                                          ?
?  ?? TenantManager (manager@yourdomain.com)              ?
?     ?? Gestiona tenants (no superadmin)                 ?
?                                                          ?
?  ?? Support (support@yourdomain.com)                    ?
?     ?? Soporte técnico (read-only+)                     ?
?                                                          ?
???????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????
?              TENANT DB #1 (my-store)                    ?
???????????????????????????????????????????????????????????
?                                                          ?
?  ?? Admin (admin@my-store)                              ?
?     ?? Administrador de su tienda                       ?
?     ?? Panel: /dashboard                                ?
?     ?? REQUIERE X-Tenant-Slug: my-store                 ?
?                                                          ?
?  ?? Manager (manager@my-store)                          ?
?     ?? Gestiona productos/órdenes                       ?
?                                                          ?
?  ?? Customer (user@example.com)                         ?
?     ?? Cliente final de la tienda                       ?
?                                                          ?
???????????????????????????????????????????????????????????

???????????????????????????????????????????????????????????
?             TENANT DB #2 (another-store)                ?
???????????????????????????????????????????????????????????
?                                                          ?
?  ?? Admin (admin@another-store)                         ?
?     ?? Administrador de SU tienda (independiente)       ?
?                                                          ?
???????????????????????????????????????????????????????????
```

---

## 1?? AdminDbSeeder (Sistema Global)

### **¿Qué hace?**

Crea los usuarios y roles del **panel administrativo global** que gestiona todos los tenants.

### **¿Cuándo se ejecuta?**

- **Automáticamente** al iniciar la aplicación (ver `Program.cs`)
- Es **idempotente** - puede ejecutarse múltiples veces sin duplicar datos

### **¿Qué crea?**

#### **Roles:**
```
SuperAdmin     ? Acceso completo al sistema
TenantManager  ? Gestión de tenants
Support        ? Soporte técnico
Viewer         ? Solo lectura
```

#### **Usuario por defecto:**
```
Email:    admin@yourdomain.com
Password: Admin123!
Rol:      SuperAdmin
```

### **Ubicación:**
```
CC.Infraestructure/Admin/AdminDbSeeder.cs
```

### **Código de implementación:**

```csharp
// Program.cs
using (var scope = app.Services.CreateScope())
{
    var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    await CC.Infraestructure.Admin.AdminDbSeeder.SeedAsync(adminDb, logger);
}
```

### **Login de SuperAdmin:**

```http
POST https://localhost:7001/admin/auth/login
Content-Type: application/json

{
  "email": "admin@yourdomain.com",
  "password": "Admin123!"
}

Response:
{
  "token": "eyJhbGc...",
  "expiresAt": "2024-12-04T...",
  "user": {
    "id": "...",
    "email": "admin@yourdomain.com",
    "fullName": "Super Administrator",
    "roles": ["SuperAdmin"]
  }
}
```

### **Crear usuarios admin adicionales:**

```csharp
await AdminDbSeeder.CreateAdminUserAsync(
    adminDb,
    email: "manager@yourdomain.com",
    password: "SecurePass123!",
    fullName: "Tenant Manager",
    roleName: AdminRoleNames.TenantManager,
    logger
);
```

---

## 2?? TenantDbSeeder (Tienda Individual)

### **¿Qué hace?**

Crea los usuarios y roles de cada **tienda individual** (tenant).

### **¿Cuándo se ejecuta?**

- **Automáticamente** al aprovisionar un nuevo tenant
- Es **idempotente** - puede ejecutarse múltiples veces sin duplicar datos

### **¿Qué crea?**

#### **Roles del Tenant:**
```
Admin    ? Administrador de la tienda (full access)
Manager  ? Manager de productos/órdenes
Customer ? Cliente regular (compra productos)
```

#### **Usuario Admin del Tenant:**
```
Email:    admin@{tenant-slug}
Password: TenantAdmin123!
Rol:      Admin
```

**Ejemplo para tenant "my-store":**
```
Email:    admin@my-store
Password: TenantAdmin123!
```

### **Ubicación:**
```
CC.Infraestructure/Tenant/TenantDbSeeder.cs
```

### **Código de implementación:**

```csharp
// TenantProvisioner.cs
await using var tenantDb = new Tenant.TenantDbContext(optionsBuilder.Options);

// Usar TenantDbSeeder
await Tenant.TenantDbSeeder.SeedAsync(tenantDb, tenant.Slug, _logger);
```

### **Login de Tenant Admin:**

```http
POST https://localhost:7001/auth/login
Headers:
  X-Tenant-Slug: my-store
Content-Type: application/json

{
  "email": "admin@my-store",
  "password": "TenantAdmin123!"
}

Response:
{
  "token": "eyJhbGc...",
  "expiresAt": "2024-12-04T...",
  "user": {
    "id": "...",
    "email": "admin@my-store",
    "firstName": "Admin",
    "lastName": "MY-STORE"
  }
}
```

### **Crear usuarios tenant adicionales:**

```csharp
await TenantDbSeeder.CreateTenantUserAsync(
    tenantDb,
    email: "manager@my-store",
    password: "SecurePass123!",
    firstName: "Store",
    lastName: "Manager",
    roleName: "Manager",
    logger
);
```

---

## ?? Tabla de Credenciales por Defecto

| Usuario | Email | Password | Endpoint | X-Tenant-Slug |
|---------|-------|----------|----------|---------------|
| **SuperAdmin** | admin@yourdomain.com | Admin123! | `/admin/auth/login` | ? NO |
| **Tenant Admin** | admin@{slug} | TenantAdmin123! | `/auth/login` | ? SÍ |

---

## ?? Diferencias entre Admin y Tenant

| Característica | Admin (Global) | Tenant (Tienda) |
|----------------|----------------|-----------------|
| **Base de Datos** | AdminDb | TenantDb |
| **Tabla de Usuarios** | `admin.AdminUsers` | `tenant.Users` |
| **Roles** | SuperAdmin, TenantManager, Support | Admin, Manager, Customer |
| **Endpoint Login** | `/admin/auth/login` | `/auth/login` |
| **Requiere X-Tenant-Slug** | ? NO | ? SÍ |
| **Gestiona** | Todos los tenants | Solo su tienda |
| **Vista Frontend** | Panel Admin Global | Panel de Tienda |
| **JWT Claim** | `"admin": "true"` | `"tenant_id": "..."` |

---

## ?? Flujo de Creación de Tenant

```
1. POST /provision/tenants/init
   ?? Crea entrada en admin.Tenants con status PENDING

2. POST /provision/tenants/confirm
   ?? Encola tenant para provisioning

3. TenantProvisioningWorker procesa:
   ?? CreateDatabase
   ?  ?? Crea base de datos física (tenant_my-store)
   ?
   ?? ApplyMigrations
   ?  ?? Aplica migraciones EF Core
   ?
   ?? SeedData
      ?? ?? TenantDbSeeder.SeedAsync()
         ?? Crea roles (Admin, Manager, Customer)
         ?? Crea usuario admin@{slug}
         ?? Crea categorías demo (opcional)

4. Tenant status ? READY
   ?? Tenant puede recibir requests
```

---

## ??? Comandos Útiles

### **Ver logs del seed:**

```sh
# Al iniciar la aplicación verás:
?? Starting AdminDb seed...
Creating admin roles...
? Created 4 admin roles
Creating SuperAdmin user...
? Created SuperAdmin user: admin@yourdomain.com
??  DEFAULT CREDENTIALS - Email: admin@yourdomain.com | Password: Admin123!
? AdminDb seed completed successfully
```

### **Regenerar seed (desarrollo):**

```sql
-- Eliminar todos los usuarios admin (SOLO DESARROLLO)
TRUNCATE TABLE admin."AdminUserRoles" CASCADE;
TRUNCATE TABLE admin."AdminUsers" CASCADE;
TRUNCATE TABLE admin."AdminRoles" CASCADE;

-- Reiniciar aplicación ? seed se ejecutará automáticamente
```

### **Cambiar contraseña de admin:**

```csharp
// Usar AdminAuthService.HashPassword para generar nuevo hash
var (newHash, newSalt) = AdminAuthService.HashPassword("NuevaContraseña123!");

// Actualizar en BD
UPDATE admin."AdminUsers" 
SET "PasswordHash" = 'NEW_HASH', 
    "PasswordSalt" = 'NEW_SALT'
WHERE "Email" = 'admin@yourdomain.com';
```

---

## ?? Advertencias de Seguridad

### **Producción:**

1. ? **Cambiar contraseñas por defecto** inmediatamente después del primer deploy
2. ? **Usar variables de entorno** para credenciales iniciales
3. ? **Deshabilitar seed automático** en producción (si es necesario)
4. ? **Forzar cambio de contraseña** en primer login
5. ? **Usar HTTPS** siempre
6. ? **Implementar 2FA** para SuperAdmin

### **Desarrollo:**

1. ?? Las contraseñas por defecto son aceptables
2. ?? Seed automático facilita el desarrollo
3. ?? Logs de credenciales ayudan a debugging

---

## ?? Proceso de Migración

```bash
# 1. Generar migración de AdminDb
dotnet ef migrations add AddAdminUsersAndRoles \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output-dir Admin/Migrations

# 2. Aplicar migración
dotnet ef database update \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce

# 3. Iniciar aplicación
dotnet run --project Api-eCommerce

# 4. El seed se ejecutará automáticamente
# Ver logs en consola
```

---

## ?? Referencias

- **AdminDbSeeder:** `CC.Infraestructure/Admin/AdminDbSeeder.cs`
- **TenantDbSeeder:** `CC.Infraestructure/Tenant/TenantDbSeeder.cs`
- **Program.cs:** Configuración de seed automático
- **TenantProvisioner:** Integración de TenantDbSeeder
- **AdminAuthService:** Hashing de contraseñas

---

## ? Checklist de Implementación

- [x] AdminDbSeeder creado
- [x] TenantDbSeeder creado
- [x] Program.cs actualizado con seed automático
- [x] TenantProvisioner integra TenantDbSeeder
- [x] README actualizado con credenciales
- [x] Roles definidos (Admin y Tenant)
- [x] Hashing de contraseñas implementado
- [ ] Migraciones generadas y aplicadas
- [ ] Tests de seeders
- [ ] Forzar cambio de contraseña en primer login

---

**Última actualización:** Diciembre 2024  
**Versión:** 2.0  
**Estado:** ? **Implementado y documentado**
