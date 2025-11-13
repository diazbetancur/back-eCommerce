# ?? CI/CD Pipeline y Scripts de Migración

## ?? Contenido

Este directorio contiene scripts de PowerShell para gestionar migraciones de base de datos en la arquitectura multi-tenant.

---

## ?? Scripts Disponibles

### 1. `migrate-tenant.ps1`
Aplica migraciones de EF Core a un tenant específico.

#### Parámetros

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `TenantSlug` | string | ? Sí | Slug del tenant (ej: `acme`, `demo-store`) |
| `Environment` | string | ? No | Entorno: `Development`, `Staging`, `Production` (default: `Development`) |
| `ConnectionStringTemplate` | string | ? No | Template del connection string. Si no se proporciona, usa el del entorno |
| `SkipBackup` | switch | ? No | No crea backup antes de migrar |
| `DryRun` | switch | ? No | Solo muestra migraciones pendientes sin aplicarlas |
| `BackupPath` | string | ? No | Ruta donde guardar backups (default: `./backups`) |

#### Ejemplos de Uso

**Desarrollo (local):**
```powershell
# Migrar tenant "acme" en desarrollo
.\tools\migrate-tenant.ps1 -TenantSlug "acme"

# Ver migraciones pendientes sin aplicarlas
.\tools\migrate-tenant.ps1 -TenantSlug "acme" -DryRun

# Migrar sin crear backup
.\tools\migrate-tenant.ps1 -TenantSlug "demo-store" -SkipBackup
```

**Staging:**
```powershell
# Migrar en staging (usa connection string del entorno)
.\tools\migrate-tenant.ps1 -TenantSlug "acme" -Environment "Staging"
```

**Producción:**
```powershell
# Migrar en producción (requiere confirmación manual)
.\tools\migrate-tenant.ps1 -TenantSlug "acme" -Environment "Production"
```

**Connection String personalizado:**
```powershell
.\tools\migrate-tenant.ps1 `
  -TenantSlug "acme" `
  -ConnectionStringTemplate "Host=custom-host;Database={DBNAME};User Id=user;Password=pass"
```

---

### 2. `migrate-all-tenants.ps1`
Aplica migraciones a **TODOS** los tenants activos.

#### Parámetros

| Parámetro | Tipo | Requerido | Descripción |
|-----------|------|-----------|-------------|
| `Environment` | string | ? No | Entorno: `Development`, `Staging`, `Production` |
| `AdminConnectionString` | string | ? No | Connection string de Admin DB |
| `TenantConnectionStringTemplate` | string | ? No | Template para tenants |
| `SkipBackup` | switch | ? No | No crea backups |
| `DryRun` | switch | ? No | Solo lista tenants sin migrar |
| `ContinueOnError` | switch | ? No | Continúa si un tenant falla |
| `MaxParallel` | int | ? No | Máximo de migraciones paralelas (default: 1) |

#### Ejemplos de Uso

**Ver todos los tenants activos:**
```powershell
.\tools\migrate-all-tenants.ps1 -DryRun
```

**Migrar todos los tenants en desarrollo:**
```powershell
.\tools\migrate-all-tenants.ps1
```

**Migrar todos en producción (continuar si falla uno):**
```powershell
.\tools\migrate-all-tenants.ps1 -Environment "Production" -ContinueOnError
```

**Migrar todos sin backups (NO RECOMENDADO en producción):**
```powershell
.\tools\migrate-all-tenants.ps1 -SkipBackup
```

---

## ?? Prerequisitos

### Software Requerido

1. **.NET 8 SDK**
   ```powershell
   dotnet --version
   # Debe ser 8.0.x
   ```

2. **Entity Framework Core Tools**
   ```powershell
   dotnet tool install --global dotnet-ef --version 8.0.*
   
   # Verificar
   dotnet ef --version
   ```

3. **PostgreSQL Client Tools** (para backups)
   ```powershell
   # Windows: Instalar PostgreSQL
   # O usar chocolatey:
   choco install postgresql
   
   # Verificar
   psql --version
   pg_dump --version
   ```

4. **PowerShell 7+** (recomendado)
   ```powershell
   $PSVersionTable.PSVersion
   ```

---

## ?? Configuración

### Connection Strings por Entorno

Los scripts usan templates de connection string configurados internamente:

**Development:**
```
Host=localhost;Database={DBNAME};User Id=postgres;Password=postgres;TrustServerCertificate=true
```

**Staging:**
```
Host=staging-db.example.com;Database={DBNAME};User Id=api_user;Password=$env:STAGING_DB_PASSWORD;TrustServerCertificate=true
```

**Production:**
```
Host=prod-db.example.com;Database={DBNAME};User Id=api_user;Password=$env:PROD_DB_PASSWORD;TrustServerCertificate=true;Pooling=true
```

### Variables de Entorno

Para Staging y Production, configura estas variables:

```powershell
# Windows
$env:STAGING_DB_PASSWORD = "tu-password-staging"
$env:PROD_DB_PASSWORD = "tu-password-produccion"

# Linux/Mac
export STAGING_DB_PASSWORD="tu-password-staging"
export PROD_DB_PASSWORD="tu-password-produccion"
```

---

## ??? Backups

### Automáticos

Por defecto, los scripts crean backups automáticos antes de aplicar migraciones:

- **Ubicación:** `./backups/`
- **Formato:** `tenant_{slug}_{environment}_{timestamp}.backup`
- **Tipo:** PostgreSQL custom format (`.backup`)

### Restaurar un Backup

```powershell
# PostgreSQL
pg_restore -h localhost -U postgres -d tenant_acme -F c backups/tenant_acme_Production_20241201_143022.backup

# Con clean (borra datos existentes primero)
pg_restore -h localhost -U postgres -d tenant_acme -c -F c backups/tenant_acme_Production_20241201_143022.backup
```

### Deshabilitar Backups

```powershell
.\tools\migrate-tenant.ps1 -TenantSlug "acme" -SkipBackup
```

?? **NO recomendado en producción**

---

## ?? Workflow de Migraciones

### 1. Desarrollo Local

```powershell
# 1. Crear nueva migración (si es necesario)
dotnet ef migrations add NombreDeLaMigracion `
  --project ./CC.Infraestructure/CC.Infraestructure.csproj `
  --startup-project ./Api-eCommerce/Api-eCommerce.csproj `
  --context CC.Infraestructure.Tenant.TenantDbContext

# 2. Ver migraciones pendientes
.\tools\migrate-tenant.ps1 -TenantSlug "acme" -DryRun

# 3. Aplicar migraciones
.\tools\migrate-tenant.ps1 -TenantSlug "acme"

# 4. Verificar que funcionó
dotnet ef migrations list `
  --project ./CC.Infraestructure/CC.Infraestructure.csproj `
  --startup-project ./Api-eCommerce/Api-eCommerce.csproj `
  --context CC.Infraestructure.Tenant.TenantDbContext `
  --connection "Host=localhost;Database=tenant_acme;User Id=postgres;Password=postgres"
```

### 2. Staging

```powershell
# 1. Verificar migraciones pendientes
.\tools\migrate-tenant.ps1 -TenantSlug "acme" -Environment "Staging" -DryRun

# 2. Aplicar a un tenant de prueba primero
.\tools\migrate-tenant.ps1 -TenantSlug "test-tenant" -Environment "Staging"

# 3. Si todo OK, aplicar a todos
.\tools\migrate-all-tenants.ps1 -Environment "Staging"
```

### 3. Producción

```powershell
# 1. DRY RUN para ver qué se migrará
.\tools\migrate-all-tenants.ps1 -Environment "Production" -DryRun

# 2. Aplicar a un tenant de menor impacto primero
.\tools\migrate-tenant.ps1 -TenantSlug "tenant-pequeño" -Environment "Production"

# 3. Si todo OK, aplicar a todos (con backups automáticos)
.\tools\migrate-all-tenants.ps1 -Environment "Production" -ContinueOnError

# 4. Verificar en la aplicación que todo funciona
```

---

## ?? Troubleshooting

### Error: "dotnet-ef not found"

```powershell
# Instalar dotnet-ef
dotnet tool install --global dotnet-ef --version 8.0.*

# Si ya está instalado, actualizar
dotnet tool update --global dotnet-ef
```

### Error: "pg_dump not found"

```powershell
# Windows (con chocolatey)
choco install postgresql

# O descargar de: https://www.postgresql.org/download/

# Agregar al PATH:
# C:\Program Files\PostgreSQL\16\bin
```

### Error: "Cannot connect to database"

```powershell
# Verificar connection string
.\tools\migrate-tenant.ps1 -TenantSlug "acme" -DryRun -Verbose

# Probar conexión manualmente
psql -h localhost -U postgres -d tenant_acme

# Verificar que la base de datos existe
psql -h localhost -U postgres -c "\l" | findstr tenant_acme
```

### Error: "Migration already applied"

Esto es normal si la migración ya fue aplicada. EF Core es idempotente y no aplicará migraciones duplicadas.

### Error en Producción

```powershell
# 1. Verificar el backup
ls ./backups/ | Sort-Object LastWriteTime -Descending | Select-Object -First 5

# 2. Restaurar desde backup
pg_restore -h prod-db -U api_user -d tenant_acme -c -F c backups/tenant_acme_Production_20241201_143022.backup

# 3. Revisar logs
cat ./logs/migration-tenant-acme-20241201.log
```

---

## ?? GitHub Actions Pipeline

### Workflow: `.github/workflows/dotnet.yml`

El pipeline automatizado incluye:

#### Jobs Principales

1. **build-and-test**
   - Compila el código
   - Ejecuta tests unitarios
   - Genera reporte de cobertura

2. **publish-api**
   - Publica la API en Release mode
   - Genera artifact para deployment

3. **migrations-admin**
   - Genera script SQL de migraciones de Admin DB
   - Sube como artifact

4. **migrations-tenant**
   - Genera script SQL de migraciones de Tenant DB
   - Sube como artifact

5. **security-scan**
   - Ejecuta Trivy para detectar vulnerabilidades
   - Sube resultados a GitHub Security

6. **deploy-development**
   - Despliega a ambiente de desarrollo
   - Aplica migraciones de Admin DB
   - Ejecuta health check

7. **deploy-staging**
   - Despliega a staging (rama main)
   - Aplica migraciones

8. **deploy-production**
   - Despliega a producción (rama main, después de staging)
   - Requiere aprobación manual (GitHub Environments)

9. **create-release**
   - Crea release en GitHub
   - Después de deploy exitoso a producción

### Triggers

```yaml
on:
  push:
    branches: [ "main", "develop" ]
  pull_request:
    branches: [ "main", "develop" ]
  workflow_dispatch:  # Manual trigger
```

### Artifacts Generados

| Artifact | Descripción | Retention |
|----------|-------------|-----------|
| `ecommerce-api` | API compilada y lista para deployment | 30 días |
| `test-results` | Resultados de tests y cobertura | 30 días |
| `admin-db-migrations` | Script SQL de migraciones Admin | 30 días |
| `tenant-db-migrations` | Script SQL de migraciones Tenant | 30 días |

### Secrets Requeridos

Configura estos secrets en GitHub (Settings > Secrets and variables > Actions):

```
DEV_ADMIN_DB_CONNECTION     # Connection string Admin DB en Development
DEV_TENANT_DB_TEMPLATE      # Template connection string para tenants en Dev
STAGING_DB_PASSWORD         # Password DB en Staging
STAGING_ADMIN_DB_CONNECTION # Connection string Admin DB en Staging
PROD_DB_PASSWORD            # Password DB en Production
PROD_ADMIN_DB_CONNECTION    # Connection string Admin DB en Production
```

### Ejecución Manual

```
1. Ir a Actions tab en GitHub
2. Seleccionar workflow "dotnet"
3. Click en "Run workflow"
4. Seleccionar branch y environment
5. Click en "Run workflow"
```

---

## ?? Monitoreo

### Logs de Migraciones

Los scripts generan logs detallados en consola con:

- ? Operaciones exitosas (verde)
- ?? Advertencias (amarillo)
- ? Errores (rojo)
- ?? Información (azul)

### Resumen de Migración

Al finalizar, se muestra un resumen:

```
============================================
 Resumen de Migración
============================================

Tenant:           acme
Environment:      Production
Migraciones:      3 pendiente(s)

Migraciones aplicadas:
  • 20241201_AddOrderTable
  • 20241202_AddPaymentProvider
  • 20241203_AddIndexes

Backup creado:    ./backups/tenant_acme_Production_20241201_143022.backup

? Migración completada exitosamente
```

---

## ?? Seguridad

### Mejores Prácticas

1. **Siempre crear backups en producción**
   - No usar `-SkipBackup` en producción
   - Verificar que los backups se crearon correctamente

2. **Usar Dry Run primero**
   ```powershell
   .\tools\migrate-tenant.ps1 -TenantSlug "acme" -Environment "Production" -DryRun
   ```

3. **Migrar de a uno en producción**
   - Empezar con tenants pequeños
   - Verificar antes de continuar con el resto

4. **Proteger passwords**
   - Usar variables de entorno
   - No hardcodear passwords en scripts
   - Usar GitHub Secrets en CI/CD

5. **Confirmación manual en producción**
   - El script requiere escribir "SI" para confirmar
   - Para todos los tenants: "SI ESTOY SEGURO"

---

## ?? Soporte

### Archivos Relevantes

- **Pipeline**: `.github/workflows/dotnet.yml`
- **Script Individual**: `tools/migrate-tenant.ps1`
- **Script Masivo**: `tools/migrate-all-tenants.ps1`
- **Documentación**: `tools/README.md` (este archivo)

### Comandos Útiles

```powershell
# Ver ayuda de un script
Get-Help .\tools\migrate-tenant.ps1 -Detailed

# Ver ejemplos
Get-Help .\tools\migrate-tenant.ps1 -Examples

# Ver todos los parámetros
Get-Help .\tools\migrate-tenant.ps1 -Parameter *
```

### Recursos

- [Entity Framework Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [GitHub Actions](https://docs.github.com/en/actions)
- [PostgreSQL Backup](https://www.postgresql.org/docs/current/backup-dump.html)

---

**Última actualización**: Diciembre 2024  
**Versión**: 1.0.0  
**Mantenido por**: eCommerce Team
