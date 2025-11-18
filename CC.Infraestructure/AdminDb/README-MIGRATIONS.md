# Admin DB - Migraciones y Configuración

## Descripción
Este documento describe cómo ejecutar las migraciones para la Admin DB (base de datos de administración de tenants).

## Estructura de Admin DB
La Admin DB contiene:
- **Tenants**: Información de cada tenant (slug, nombre, plan, estado, feature flags)
- **TenantProvisionings**: Historial de aprovisionamiento por pasos (CreateDatabase, Migrate, Seed)
- **Plans**: Planes disponibles para tenants (Basic, Premium, Enterprise)
- **Features**: Features del sistema
- **AdminUsers**: Usuarios administradores del sistema global (SuperAdmin, TenantManager, Support)
- **AdminRoles**: Roles de administración del sistema
- **AdminUserRoles**: Relación usuarios-roles

## ?? Seed Automático

### **AdminDbSeeder** (Usuario Global)

Al iniciar la aplicación, se ejecuta automáticamente el `AdminDbSeeder` que crea:

1. **Roles Administrativos:**
   - `SuperAdmin` - Acceso completo al sistema
   - `TenantManager` - Gestión de tenants
   - `Support` - Soporte técnico
   - `Viewer` - Solo lectura

2. **Usuario SuperAdmin por defecto:**
   - **Email:** `admin@yourdomain.com`
   - **Password:** `Admin123!`
   - **Rol:** SuperAdmin

?? **IMPORTANTE:** Cambiar la contraseña después del primer login en producción.

### **TenantDbSeeder** (Usuario por Tenant)

Al crear un tenant, se ejecuta automáticamente el `TenantDbSeeder` que crea:

1. **Roles del Tenant:**
   - `Admin` - Administrador de la tienda
   - `Manager` - Manager de productos/órdenes
   - `Customer` - Cliente regular

2. **Usuario Admin del Tenant:**
   - **Email:** `admin@{tenant-slug}`
   - **Password:** `TenantAdmin123!`
   - **Rol:** Admin

Ejemplo para tenant "my-store":
- **Email:** `admin@my-store`
- **Password:** `TenantAdmin123!`

## Configuración de Connection Strings

### Development (appsettings.Development.json)
```json
{
  "ConnectionStrings": {
    "AdminDb": "Host=localhost;Database=ecommerce_admin;Username=postgres;Password=postgres;"
  },
  "Tenancy": {
    "TenantDbTemplate": "Host=localhost;Database={DbName};Username=postgres;Password=postgres;",
    "AdminDb": "Host=localhost;Database=ecommerce_admin;Username=postgres;Password=postgres;"
  }
}
```

### Production (appsettings.json)
```json
{
  "ConnectionStrings": {
    "AdminDb": "Host=your-host;Port=5432;Username=your-user;Password=your-pass;Database=ecommerce_admin;SSL Mode=Require;"
  },
  "Tenancy": {
    "TenantDbTemplate": "Host=your-host;Port=5432;Username=your-user;Password=your-pass;Database={DbName};SSL Mode=Require;",
    "AdminDb": "Host=your-host;Port=5432;Username=your-user;Password=your-pass;Database=ecommerce_admin;SSL Mode=Require;"
  }
}
```

## Crear Migración Inicial

Desde la raíz del proyecto, ejecuta:

```bash
dotnet ef migrations add InitialAdminDb --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce --output-dir Admin/Migrations
```

## Aplicar Migraciones

### Desarrollo
```bash
dotnet ef database update --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

### Producción (con connection string específica)
```bash
dotnet ef database update --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce --connection "Host=your-host;Database=ecommerce_admin;..."
```

## Verificar Migraciones

```bash
# Listar migraciones aplicadas
dotnet ef migrations list --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce

# Ver el SQL generado sin aplicar
dotnet ef migrations script --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

## Rollback de Migraciones

```bash
# Revertir a una migración específica
dotnet ef database update <MigrationName> --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce

# Revertir todo (volver al estado inicial)
dotnet ef database update 0 --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

## Estructura de Esquema Admin

El AdminDbContext utiliza el schema `admin` para separar sus tablas:

```sql
-- Tablas creadas en schema admin:
admin.Tenants
admin.TenantProvisionings
admin.Plans
admin.Features
admin.PlanFeatures
admin.TenantFeatureOverrides
admin.TenantUsageDaily
admin.AdminUsers           -- NUEVO
admin.AdminRoles           -- NUEVO
admin.AdminUserRoles       -- NUEVO
admin.__EFMigrationsHistory
```

## Seed Manual (Opcional)

Si necesitas crear usuarios administrativos adicionales manualmente:

```sql
-- Crear un nuevo admin (necesitas generar hash/salt primero)
INSERT INTO admin."AdminUsers" ("Id", "Email", "PasswordHash", "PasswordSalt", "FullName", "IsActive", "CreatedAt")
VALUES 
  (gen_random_uuid(), 'manager@yourdomain.com', 'HASH_AQUI', 'SALT_AQUI', 'Manager User', true, NOW());

-- Asignar rol TenantManager
INSERT INTO admin."AdminUserRoles" ("AdminUserId", "AdminRoleId", "AssignedAt")
SELECT 
  (SELECT "Id" FROM admin."AdminUsers" WHERE "Email" = 'manager@yourdomain.com'),
  (SELECT "Id" FROM admin."AdminRoles" WHERE "Name" = 'TenantManager'),
  NOW();
```

## ?? Credenciales por Defecto

### **Sistema Global (AdminDb)**
```
URL: https://localhost:7001/admin/auth/login
Email: admin@yourdomain.com
Password: Admin123!
```

### **Tenant Individual (TenantDb)**
```
URL: https://localhost:7001/auth/login
Headers: X-Tenant-Slug: my-store
Email: admin@my-store
Password: TenantAdmin123!
```

## ?? Diferencias entre Admin y Tenant

| Característica | Admin (Global) | Tenant (Tienda) |
|----------------|----------------|-----------------|
| **Base de Datos** | AdminDb | TenantDb (una por tenant) |
| **Endpoint Login** | `/admin/auth/login` | `/auth/login` |
| **Requiere X-Tenant-Slug** | ? NO | ? SÍ |
| **Gestiona** | Todos los tenants | Solo su tienda |
| **Roles** | SuperAdmin, TenantManager, Support | Admin, Manager, Customer |
| **Vista** | Panel global | Panel de tienda |

## Notas Importantes

1. **Schema Separation**: Todas las tablas de Admin DB usan el schema `admin`
2. **Migrations History**: Se guarda en `admin.__EFMigrationsHistory`
3. **JSONB**: El campo `FeatureFlagsJson` usa tipo JSONB de PostgreSQL
4. **Índices**: El campo `Slug` tiene índice único para garantizar unicidad
5. **Cascada**: TenantProvisionings se eliminan en cascada al eliminar un Tenant
6. **Seeders**: Se ejecutan automáticamente al iniciar la aplicación (AdminDbSeeder) y al crear un tenant (TenantDbSeeder)
7. **Passwords**: Las contraseñas por defecto deben cambiarse en producción
8. **Idempotencia**: Los seeders son idempotentes (pueden ejecutarse múltiples veces sin duplicar datos)

## Troubleshooting

### Error: "Schema admin does not exist"
```sql
CREATE SCHEMA IF NOT EXISTS admin;
```

### Error: "relation does not exist"
Verificar que las migraciones se hayan aplicado:
```bash
dotnet ef migrations list --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

### Error: "Admin user already exists"
Esto es normal - el seeder es idempotente. Los usuarios no se duplicarán.

### Resetear completamente Admin DB
```bash
# Eliminar todas las migraciones
dotnet ef database drop --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce --force

# Recrear con nueva migración
dotnet ef database update --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

### Ver logs del seed
Los logs del seed aparecerán en la consola al iniciar la aplicación:
```
?? Seeding AdminDb...
Creating admin roles...
? Created 4 admin roles
Creating SuperAdmin user...
? Created SuperAdmin user: admin@yourdomain.com
??  DEFAULT CREDENTIALS - Email: admin@yourdomain.com | Password: Admin123!
??  IMPORTANT: Change the password after first login in production!
? AdminDb seed completed successfully
