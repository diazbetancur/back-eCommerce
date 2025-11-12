# Admin DB - Migraciones y Configuración

## Descripción
Este documento describe cómo ejecutar las migraciones para la Admin DB (base de datos de administración de tenants).

## Estructura de Admin DB
La Admin DB contiene:
- **Tenants**: Información de cada tenant (slug, nombre, plan, estado, feature flags)
- **TenantProvisionings**: Historial de aprovisionamiento por pasos (CreateDatabase, Migrate, Seed)

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
dotnet ef migrations add InitialAdminDb --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce --output-dir AdminDb/Migrations
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
admin.__EFMigrationsHistory
```

## Seed de Datos (Opcional)

Para crear datos de demostración, puedes ejecutar manualmente:

```sql
INSERT INTO admin."Tenants" 
("Id", "Slug", "Name", "Plan", "DbName", "Status", "CreatedAt")
VALUES 
(gen_random_uuid(), 'demo', 'Demo Tenant', 'Basic', 'ecommerce_tenant_demo', 'Active', CURRENT_TIMESTAMP);
```

O crear un seeder programático en `CC.Infraestructure/AdminDb/AdminDbSeeder.cs`.

## Notas Importantes

1. **Schema Separation**: Todas las tablas de Admin DB usan el schema `admin`
2. **Migrations History**: Se guarda en `admin.__EFMigrationsHistory`
3. **JSONB**: El campo `FeatureFlagsJson` usa tipo JSONB de PostgreSQL
4. **Índices**: El campo `Slug` tiene índice único para garantizar unicidad
5. **Cascada**: TenantProvisionings se eliminan en cascada al eliminar un Tenant

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

### Resetear completamente Admin DB
```bash
# Eliminar todas las migraciones
dotnet ef database drop --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce --force

# Recrear con nueva migración
dotnet ef database update --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```
