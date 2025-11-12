# Implementación de Admin DB y Tenancy Global - Resumen Completo

## ? Archivos Creados

### 1. Entidades de Dominio (CC.Domain/Tenancy/)
- **Tenant.cs**: Entidad principal que representa un tenant
  - Propiedades: Id, Slug (único), Name, Plan, DbName, Status, FeatureFlagsJson, CreatedAt, UpdatedAt, LastError
  - Relación con TenantProvisioning (historial)
  
- **TenantProvisioning.cs**: Historial de aprovisionamiento por pasos
  - Propiedades: Id, TenantId, Step (CreateDatabase/Migrate/Seed), Status, Message, StartedAt, CompletedAt, ErrorMessage
  - Relación con Tenant (foreign key)

### 2. DbContext para Admin DB (CC.Infraestructure/AdminDb/)
- **AdminDbContext.cs**: Context separado para base de datos de administración
  - DbSet<Tenant> Tenants
  - DbSet<TenantProvisioning> TenantProvisionings
  - Usa alias para evitar conflictos de nombres con namespace Tenant
  - Aplica configuraciones desde assembly automáticamente

### 3. Configuraciones Fluent API (CC.Infraestructure/AdminDb/Configurations/)
- **TenantConfiguration.cs**
  - Schema: admin
  - Índice único en Slug
  - FeatureFlagsJson como JSONB (PostgreSQL)
  - Relación cascada con TenantProvisioning
  - Valores por defecto: Status="Pending", CreatedAt=CURRENT_TIMESTAMP
  
- **TenantProvisioningConfiguration.cs**
  - Schema: admin
  - Índices: TenantId, (TenantId, Step) compuesto
  - Valores por defecto: Status="Pending", StartedAt=CURRENT_TIMESTAMP

### 4. Documentación (CC.Infraestructure/AdminDb/)
- **README-MIGRATIONS.md**: Guía completa de migraciones
  - Comandos EF Core
  - Configuración de connection strings
  - Troubleshooting
  - Scripts SQL útiles

### 5. Configuración de Aplicación
- **appsettings.json** (Producción)
  - ConnectionStrings:AdminDb
  - Tenancy:TenantDbTemplate (con placeholder {DbName})
  - Tenancy:AdminDb
  
- **appsettings.Development.json** (Desarrollo)
  - ConnectionStrings:AdminDb (localhost)
  - Tenancy:TenantDbTemplate
  - Logging configurado para EF Core

### 6. Program.cs
- Registro de AdminDbContext con schema admin
- Configuración de migrations history en schema admin
- Middlewares de tenancy configurados

## ?? Características Implementadas

### Entidad Tenant
```csharp
- Id: Guid (PK)
- Slug: string (unique, max 50) - Identificador para URLs
- Name: string (max 200) - Nombre del tenant
- Plan: string? (max 50) - Basic/Premium/Enterprise
- DbName: string (max 100) - Nombre de la base de datos
- Status: string (max 20) - Pending/Active/Suspended/Failed
- FeatureFlagsJson: string? - Feature flags en formato JSON (JSONB)
- CreatedAt: DateTime
- UpdatedAt: DateTime?
- LastError: string? - Último error registrado
```

### Entidad TenantProvisioning
```csharp
- Id: Guid (PK)
- TenantId: Guid (FK)
- Step: string (max 50) - CreateDatabase/Migrate/Seed
- Status: string (max 20) - Pending/InProgress/Success/Failed
- Message: string? - Detalle del paso
- StartedAt: DateTime
- CompletedAt: DateTime?
- ErrorMessage: string? - Error si falló
```

## ?? Cómo Usar

### 1. Crear Migración Inicial
```bash
cd D:\Proyects\eCommerce\back.eCommerce

dotnet ef migrations add InitialAdminDb `
  --context AdminDbContext `
  --project CC.Infraestructure `
  --startup-project Api-eCommerce `
  --output-dir AdminDb/Migrations
```

### 2. Aplicar Migraciones
```bash
# Desarrollo
dotnet ef database update `
  --context AdminDbContext `
  --project CC.Infraestructure `
  --startup-project Api-eCommerce

# Producción (con connection string específica)
dotnet ef database update `
  --context AdminDbContext `
  --project CC.Infraestructure `
  --startup-project Api-eCommerce `
  --connection "Host=idt-posgresql-diazbetancur.g.aivencloud.com;Port=19544;Database=ecommerce_admin;..."
```

### 3. Verificar Migraciones
```bash
# Listar migraciones
dotnet ef migrations list `
  --context AdminDbContext `
  --project CC.Infraestructure `
  --startup-project Api-eCommerce

# Ver SQL sin aplicar
dotnet ef migrations script `
  --context AdminDbContext `
  --project CC.Infraestructure `
  --startup-project Api-eCommerce
```

## ?? Configuración de Connection Strings

### Desarrollo (localhost)
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

### Producción (Aiven)
```json
{
  "ConnectionStrings": {
    "AdminDb": "Host=idt-posgresql-diazbetancur.g.aivencloud.com;Port=19544;Username=avnadmin;Password=AVNS_FtU8SNudd2RspD7f0fP;Database=ecommerce_admin;SSL Mode=Require;Trust Server Certificate=true"
  },
  "Tenancy": {
    "TenantDbTemplate": "Host=idt-posgresql-diazbetancur.g.aivencloud.com;Port=19544;Username=avnadmin;Password=AVNS_FtU8SNudd2RspD7f0fP;Database={DbName};SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

## ?? Estructura de Base de Datos

### Schema: admin
```sql
admin.Tenants
  - Id (uuid, PK)
  - Slug (varchar(50), UNIQUE)
  - Name (varchar(200))
  - Plan (varchar(50), nullable)
  - DbName (varchar(100))
  - Status (varchar(20), default 'Pending')
  - FeatureFlagsJson (jsonb, nullable)
  - CreatedAt (timestamp, default CURRENT_TIMESTAMP)
  - UpdatedAt (timestamp, nullable)
  - LastError (text, nullable)

admin.TenantProvisionings
  - Id (uuid, PK)
  - TenantId (uuid, FK -> Tenants.Id)
  - Step (varchar(50))
  - Status (varchar(20), default 'Pending')
  - Message (text, nullable)
  - StartedAt (timestamp, default CURRENT_TIMESTAMP)
  - CompletedAt (timestamp, nullable)
  - ErrorMessage (text, nullable)

admin.__EFMigrationsHistory
  - MigrationId
  - ProductVersion
```

### Índices
- `IX_Tenants_Slug` (UNIQUE) - Para búsquedas rápidas por slug
- `IX_TenantProvisionings_TenantId` - Para consultar historial de un tenant
- `IX_TenantProvisionings_TenantId_Step` - Para buscar pasos específicos de aprovisionamiento

## ?? Próximos Pasos

### 1. Ejecutar Migraciones
```bash
dotnet ef migrations add InitialAdminDb --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce --output-dir AdminDb/Migrations
dotnet ef database update --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

### 2. Crear Seed de Datos Demo (Opcional)
```sql
INSERT INTO admin."Tenants" 
("Id", "Slug", "Name", "Plan", "DbName", "Status", "FeatureFlagsJson", "CreatedAt")
VALUES 
(gen_random_uuid(), 'demo', 'Demo Tenant', 'Basic', 'ecommerce_tenant_demo', 'Active', '{"feature1": true, "feature2": false}', CURRENT_TIMESTAMP),
(gen_random_uuid(), 'test', 'Test Tenant', 'Premium', 'ecommerce_tenant_test', 'Active', '{"feature1": true, "feature2": true}', CURRENT_TIMESTAMP);
```

### 3. Implementar Endpoints de Gestión de Tenants
- POST /api/admin/tenants - Crear nuevo tenant
- GET /api/admin/tenants - Listar tenants
- GET /api/admin/tenants/{slug} - Obtener tenant por slug
- PUT /api/admin/tenants/{slug} - Actualizar tenant
- DELETE /api/admin/tenants/{slug} - Eliminar tenant
- GET /api/admin/tenants/{slug}/provisioning - Ver historial de aprovisionamiento

### 4. Implementar Lógica de Aprovisionamiento
- Crear base de datos del tenant
- Ejecutar migraciones en DB del tenant
- Ejecutar seed de datos iniciales
- Registrar cada paso en TenantProvisioning

## ?? Notas Importantes

1. **Separación de Contextos**: AdminDbContext es completamente independiente de DBContext (main app)
2. **Schema Separation**: Usa schema `admin` para separar tablas de administración
3. **JSONB**: FeatureFlagsJson usa JSONB de PostgreSQL para almacenamiento eficiente
4. **Índice Único**: El Slug tiene índice único para garantizar no duplicados
5. **Cascada**: TenantProvisionings se eliminan automáticamente al eliminar un Tenant
6. **Template**: TenantDbTemplate usa placeholder {DbName} que se reemplaza dinámicamente
7. **Migrations History**: Se guarda en admin.__EFMigrationsHistory para mantener separación

## ?? Troubleshooting

### Schema no existe
```sql
CREATE SCHEMA IF NOT EXISTS admin;
```

### Resetear Admin DB completamente
```bash
dotnet ef database drop --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce --force
dotnet ef database update --context AdminDbContext --project CC.Infraestructure --startup-project Api-eCommerce
```

### Verificar conexión
```bash
psql -h idt-posgresql-diazbetancur.g.aivencloud.com -p 19544 -U avnadmin -d ecommerce_admin
```

## ? Validación

El proyecto compila correctamente sin errores:
- ? Entidades creadas
- ? DbContext configurado
- ? Configuraciones Fluent API implementadas
- ? Program.cs actualizado
- ? appsettings configurados
- ? Documentación de migraciones creada
- ? Build exitoso

---

**Autor**: Sistema de IA
**Fecha**: 2025
**Versión**: 1.0
