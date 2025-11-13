# Guía de Migraciones EF Core - Multi-Tenant

## Descripción General

Este proyecto mantiene dos pipelines de migraciones EF Core separados:
1. **AdminDbContext**: Base de datos central de administración (tenants, planes, features)
2. **TenantDbContext**: Base de datos por tenant (catálogo, carrito, pedidos, usuarios)

## Estructura de Migraciones

```
CC.Infraestructure/
??? AdminDb/
?   ??? AdminDbContext.cs
?   ??? Migrations/
?       ??? (migraciones de Admin DB)
??? Tenant/
?   ??? TenantDbContext.cs
?   ??? Migrations/
?       ??? (migraciones de Tenant DB)
??? EF/
    ??? MigrationRunner.cs (helper para aplicar migraciones en runtime)
```

## Comandos de Migraciones

### Admin DB

#### Crear Nueva Migración
```bash
dotnet ef migrations add <NombreMigracion> \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output-dir AdminDb/Migrations
```

**Ejemplo:**
```bash
dotnet ef migrations add AddTenantUsageTable \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output-dir AdminDb/Migrations
```

#### Aplicar Migraciones
```bash
# Aplicar todas las migraciones pendientes
dotnet ef database update \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce

# Aplicar hasta una migración específica
dotnet ef database update <NombreMigracion> \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce

# Revertir todas las migraciones
dotnet ef database update 0 \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce
```

#### Listar Migraciones
```bash
dotnet ef migrations list \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce
```

#### Generar Script SQL
```bash
# Script completo
dotnet ef migrations script \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output admin-migrations.sql

# Script incremental (desde una migración a otra)
dotnet ef migrations script <FromMigration> <ToMigration> \
  --context AdminDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output admin-incremental.sql
```

### Tenant DB

#### Crear Nueva Migración
```bash
dotnet ef migrations add <NombreMigracion> \
  --context TenantDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output-dir Tenant/Migrations
```

**Ejemplo:**
```bash
dotnet ef migrations add InitialTenantSchema \
  --context TenantDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output-dir Tenant/Migrations
```

#### Aplicar Migraciones en Dev (Base de Datos Específica)
```bash
# NO aplicar directamente en producción - usar MigrationRunner
dotnet ef database update \
  --context TenantDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --connection "Host=localhost;Database=ecom_tenant_test;Username=postgres;Password=postgres;"
```

#### Listar Migraciones
```bash
dotnet ef migrations list \
  --context TenantDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce
```

#### Generar Script SQL
```bash
dotnet ef migrations script \
  --context TenantDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --output tenant-migrations.sql
```

## MigrationRunner - Aplicar Migraciones en Runtime

### Interfaz IMigrationRunner

```csharp
public interface IMigrationRunner
{
    // Admin DB
    Task<bool> ApplyAdminMigrationsAsync(CancellationToken cancellationToken = default);
    Task<bool> HasPendingAdminMigrationsAsync(CancellationToken cancellationToken = default);
    
    // Tenant DB
    Task<bool> ApplyTenantMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<bool> HasPendingTenantMigrationsAsync(string connectionString, CancellationToken cancellationToken = default);
}
```

### Uso en TenantProvisioner

El `TenantProvisioner` usa `IMigrationRunner` automáticamente durante el aprovisionamiento:

```csharp
public class TenantProvisioner : ITenantProvisioner
{
    private readonly IMigrationRunner _migrationRunner;
    
    private async Task ApplyMigrationsStepAsync(Tenant tenant, CancellationToken ct)
    {
        var connectionString = GetTenantConnectionString(tenant.DbName);
        
        // Aplicar migraciones de TenantDb
        var success = await _migrationRunner.ApplyTenantMigrationsAsync(
            connectionString, 
            ct);
        
        if (!success)
        {
            throw new Exception("Failed to apply tenant migrations");
        }
    }
}
```

### Uso Manual en Controladores/Endpoints

```csharp
[HttpPost("admin/apply-migrations")]
public async Task<IActionResult> ApplyMigrations(
    [FromServices] IMigrationRunner migrationRunner)
{
    // Aplicar migraciones de Admin DB
    var success = await migrationRunner.ApplyAdminMigrationsAsync();
    
    if (success)
        return Ok(new { message = "Admin migrations applied successfully" });
    else
        return Problem("Failed to apply admin migrations");
}

[HttpPost("admin/tenant/{slug}/apply-migrations")]
public async Task<IActionResult> ApplyTenantMigrations(
    string slug,
    [FromServices] IMigrationRunner migrationRunner,
    [FromServices] AdminDbContext adminDb)
{
    var tenant = await adminDb.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
    if (tenant == null)
        return NotFound();
    
    var connectionString = GetConnectionString(tenant.DbName);
    var success = await migrationRunner.ApplyTenantMigrationsAsync(connectionString);
    
    if (success)
        return Ok(new { message = $"Migrations applied to tenant {slug}" });
    else
        return Problem($"Failed to apply migrations to tenant {slug}");
}
```

### Uso en Startup/Health Checks

```csharp
// En Program.cs - verificar migraciones al iniciar
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var migrationRunner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    
    // Verificar y aplicar migraciones de Admin DB automáticamente en dev
    if (await migrationRunner.HasPendingAdminMigrationsAsync())
    {
        app.Logger.LogWarning("Admin DB has pending migrations. Applying...");
        await migrationRunner.ApplyAdminMigrationsAsync();
    }
}
```

## Esquemas de Base de Datos

### AdminDbContext
- **Schema**: `admin`
- **Tablas**:
  - `admin.Tenants`: Información de tenants
  - `admin.TenantProvisionings`: Historial de aprovisionamiento
  - `admin.Plans`: Planes disponibles
  - `admin.Features`: Features disponibles
  - `admin.PlanFeatures`: Features por plan
  - `admin.TenantFeatureOverrides`: Overrides por tenant
  - `admin.TenantUsageDaily`: Métricas diarias

### TenantDbContext
- **Schema**: `public`
- **Tablas**:
  - **Auth**: `Users`, `Roles`, `UserRoles`
  - **Config**: `Settings`, `WebPushSubscriptions`
  - **Catalog**: `Products`, `Categories`, `ProductCategories`, `ProductImages`
  - **Cart**: `Carts`, `CartItems`
  - **Orders**: `Orders`, `OrderItems`, `OrderStatuses`

## Flujo de Aprovisionamiento con Migraciones

```
1. POST /provision/tenants/init
   ?
2. Crear registro en admin.Tenants (Status: PENDING_VALIDATION)
   ?
3. POST /provision/tenants/confirm
   ?
4. Worker encola tenant
   ?
5. TenantProvisioner.ProvisionTenantAsync()
   ?? CreateDatabaseAsync() ? Crea BD PostgreSQL
   ?? ApplyMigrationsAsync() ? Aplica migraciones EF
   ?  ?? MigrationRunner.ApplyTenantMigrationsAsync()
   ?     ?? context.Database.MigrateAsync()
   ?? SeedDataAsync() ? Roles, Admin, Settings, OrderStatuses
   ?
6. Status = Active
```

## Buenas Prácticas

### ? DO
- Crear migraciones con nombres descriptivos
- Probar migraciones en entorno de desarrollo antes de producción
- Generar scripts SQL para revisión manual en producción crítica
- Usar `MigrationRunner` para aplicar migraciones en tenants durante aprovisionamiento
- Mantener separados los contextos Admin y Tenant
- Usar transacciones cuando sea necesario

### ? DON'T
- No aplicar migraciones directamente en producción sin probar
- No mezclar migraciones de Admin y Tenant en el mismo directorio
- No aplicar `database update` manualmente en bases de tenants (usar MigrationRunner)
- No eliminar migraciones que ya fueron aplicadas en producción
- No modificar migraciones existentes después de aplicarlas

## Rollback de Migraciones

### Admin DB
```bash
# Ver historial de migraciones
dotnet ef migrations list --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce

# Revertir a una migración específica
dotnet ef database update <MigrationName> --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce

# Revertir todo
dotnet ef database update 0 --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

### Tenant DB
```bash
# Para desarrollo (con connection string específica)
dotnet ef database update <MigrationName> \
  --context TenantDbContext \
  --project CC.Infraestructure \
  --startup-project Api-eCommerce \
  --connection "Host=localhost;Database=ecom_tenant_test;..."
```

Para producción, crear un endpoint o script que use MigrationRunner con una migración target específica.

## Troubleshooting

### Error: "The migration <name> has already been applied"
**Solución**: La migración ya fue aplicada. Listar migraciones para verificar.

### Error: "No migrations were found"
**Solución**: 
1. Verificar que la migración existe en el directorio correcto
2. Verificar que el proyecto se compiló después de crear la migración
3. Limpiar y reconstruir: `dotnet clean && dotnet build`

### Error: "Cannot connect to database"
**Solución**: Verificar connection string en appsettings.json

### Migraciones pendientes después de aprovisionamiento
**Solución**: Revisar logs del TenantProvisioner. Verificar que MigrationRunner se ejecutó correctamente.

## Testing

### Unit Test - MigrationRunner
```csharp
[Test]
public async Task ApplyTenantMigrations_ValidConnectionString_ReturnsTrue()
{
    var connectionString = "Host=localhost;Database=test_tenant;...";
    var success = await _migrationRunner.ApplyTenantMigrationsAsync(connectionString);
    Assert.IsTrue(success);
}
```

### Integration Test - Provisioning
```csharp
[Test]
public async Task ProvisionTenant_AppliesMigrationsSuccessfully()
{
    // Crear tenant
    var tenant = new Tenant { Slug = "test", DbName = "ecom_tenant_test" };
    
    // Aprovisionar
    var success = await _provisioner.ProvisionTenantAsync(tenant.Id);
    
    // Verificar que no hay migraciones pendientes
    var hasPending = await _migrationRunner.HasPendingTenantMigrationsAsync(connectionString);
    Assert.IsFalse(hasPending);
}
```

## Referencias

- [EF Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [EF Core Migration Script Generation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#sql-scripts)
- [Applying Migrations at Runtime](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#apply-migrations-at-runtime)

---

**Autor**: Sistema de IA
**Fecha**: 2025-01-10
**Versión**: 1.0
