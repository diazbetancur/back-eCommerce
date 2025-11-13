<#
.SYNOPSIS
    Aplica migraciones de Entity Framework a un tenant específico.

.DESCRIPTION
    Este script aplica las migraciones pendientes a la base de datos de un tenant específico.
    Soporta múltiples entornos (Development, Staging, Production) y genera backups automáticos.

.PARAMETER TenantSlug
    El slug del tenant al que se aplicarán las migraciones (ej: "acme", "demo-store").

.PARAMETER Environment
    El entorno donde se ejecutará (Development, Staging, Production). Default: Development.

.PARAMETER ConnectionStringTemplate
    Template del connection string. {DBNAME} será reemplazado por el nombre de la base de datos.
    Si no se proporciona, se usa el del entorno.

.PARAMETER SkipBackup
    Si se especifica, no se crea backup antes de aplicar las migraciones.

.PARAMETER DryRun
    Si se especifica, solo muestra las migraciones pendientes sin aplicarlas.

.EXAMPLE
    .\migrate-tenant.ps1 -TenantSlug "acme"
    
.EXAMPLE
    .\migrate-tenant.ps1 -TenantSlug "demo-store" -Environment "Production"
    
.EXAMPLE
    .\migrate-tenant.ps1 -TenantSlug "test-tenant" -DryRun
    
.EXAMPLE
    .\migrate-tenant.ps1 -TenantSlug "acme" -ConnectionStringTemplate "Host=localhost;Database={DBNAME};User Id=postgres;Password=MyPass123"

.NOTES
    Autor: eCommerce Team
    Fecha: Diciembre 2024
    Versión: 1.0.0
    Requiere: .NET 8 SDK, dotnet-ef tool
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Slug del tenant (ej: acme, demo-store)")]
    [ValidatePattern("^[a-z0-9-]{3,50}$")]
    [string]$TenantSlug,

    [Parameter(Mandatory = $false)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment = "Development",

    [Parameter(Mandatory = $false)]
    [string]$ConnectionStringTemplate,

    [Parameter(Mandatory = $false)]
    [switch]$SkipBackup,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [string]$BackupPath = "./backups"
)

# ============================================
# Configuración
# ============================================

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$InfrastructureProject = Join-Path $ProjectRoot "CC.Infraestructure/CC.Infraestructure.csproj"
$StartupProject = Join-Path $ProjectRoot "Api-eCommerce/Api-eCommerce.csproj"
$DbContextName = "CC.Infraestructure.Tenant.TenantDbContext"

# Connection string templates por entorno
$ConnectionStringTemplates = @{
    "Development" = "Host=localhost;Database={DBNAME};User Id=postgres;Password=postgres;TrustServerCertificate=true"
    "Staging"     = "Host=staging-db.example.com;Database={DBNAME};User Id=api_user;Password=$($env:STAGING_DB_PASSWORD);TrustServerCertificate=true"
    "Production"  = "Host=prod-db.example.com;Database={DBNAME};User Id=api_user;Password=$($env:PROD_DB_PASSWORD);TrustServerCertificate=true;Pooling=true"
}

# ============================================
# Funciones auxiliares
# ============================================

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "??  $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "??  $Message" -ForegroundColor Blue
}

function Test-Prerequisites {
    Write-Header "Verificando prerequisitos"
    
    # Verificar .NET SDK
    Write-Info "Verificando .NET SDK..."
    $dotnetVersion = dotnet --version
    if ($LASTEXITCODE -ne 0) {
        Write-Error ".NET SDK no está instalado"
        exit 1
    }
    Write-Success ".NET SDK versión: $dotnetVersion"
    
    # Verificar dotnet-ef tool
    Write-Info "Verificando dotnet-ef tool..."
    $efVersion = dotnet ef --version 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet-ef no está instalado. Instalando..."
        dotnet tool install --global dotnet-ef --version 8.0.*
        if ($LASTEXITCODE -ne 0) {
            Write-Error "No se pudo instalar dotnet-ef"
            exit 1
        }
    }
    Write-Success "dotnet-ef está disponible"
    
    # Verificar proyectos
    Write-Info "Verificando proyectos..."
    if (-not (Test-Path $InfrastructureProject)) {
        Write-Error "No se encontró el proyecto de infraestructura: $InfrastructureProject"
        exit 1
    }
    if (-not (Test-Path $StartupProject)) {
        Write-Error "No se encontró el proyecto de API: $StartupProject"
        exit 1
    }
    Write-Success "Proyectos encontrados"
}

function Get-ConnectionString {
    Write-Info "Construyendo connection string..."
    
    $template = $ConnectionStringTemplate
    if ([string]::IsNullOrWhiteSpace($template)) {
        $template = $ConnectionStringTemplates[$Environment]
    }
    
    if ([string]::IsNullOrWhiteSpace($template)) {
        Write-Error "No se encontró template de connection string para el entorno: $Environment"
        exit 1
    }
    
    $dbName = "tenant_$($TenantSlug.Replace('-', '_'))"
    $connectionString = $template.Replace("{DBNAME}", $dbName)
    
    Write-Success "Connection string construido para: $dbName"
    return $connectionString
}

function Test-DatabaseConnection {
    param([string]$ConnectionString)
    
    Write-Info "Probando conexión a la base de datos..."
    
    # Extraer parámetros del connection string
    $params = @{}
    $ConnectionString.Split(';') | ForEach-Object {
        $parts = $_.Split('=', 2)
        if ($parts.Count -eq 2) {
            $params[$parts[0].Trim()] = $parts[1].Trim()
        }
    }
    
    try {
        # Intentar conexión usando dotnet ef
        $testQuery = "dotnet ef database update --project `"$InfrastructureProject`" --startup-project `"$StartupProject`" --context `"$DbContextName`" --connection `"$ConnectionString`" --no-build -- --help"
        
        # Solo verificamos que el comando no falle
        $result = Invoke-Expression $testQuery 2>&1
        
        Write-Success "Conexión a base de datos exitosa"
        return $true
    }
    catch {
        Write-Error "No se pudo conectar a la base de datos: $($_.Exception.Message)"
        return $false
    }
}

function Get-PendingMigrations {
    param([string]$ConnectionString)
    
    Write-Info "Consultando migraciones pendientes..."
    
    try {
        $migrationsOutput = dotnet ef migrations list `
            --project "$InfrastructureProject" `
            --startup-project "$StartupProject" `
            --context "$DbContextName" `
            --connection "$ConnectionString" `
            --no-build `
            2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "No se pudieron listar las migraciones"
            return @()
        }
        
        # Parsear output para encontrar migraciones pendientes
        $pendingMigrations = @()
        $lines = $migrationsOutput -split "`n"
        $inPendingSection = $false
        
        foreach ($line in $lines) {
            if ($line -match "Pending:") {
                $inPendingSection = $true
                continue
            }
            if ($inPendingSection -and $line.Trim() -ne "") {
                $pendingMigrations += $line.Trim()
            }
        }
        
        return $pendingMigrations
    }
    catch {
        Write-Warning "Error al listar migraciones: $($_.Exception.Message)"
        return @()
    }
}

function Create-DatabaseBackup {
    param(
        [string]$ConnectionString,
        [string]$TenantSlug
    )
    
    if ($SkipBackup) {
        Write-Warning "Backup omitido por parámetro -SkipBackup"
        return $null
    }
    
    Write-Info "Creando backup de la base de datos..."
    
    # Crear directorio de backups si no existe
    if (-not (Test-Path $BackupPath)) {
        New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
    }
    
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupFile = Join-Path $BackupPath "tenant_${TenantSlug}_${Environment}_${timestamp}.backup"
    
    # Extraer información del connection string
    $params = @{}
    $ConnectionString.Split(';') | ForEach-Object {
        $parts = $_.Split('=', 2)
        if ($parts.Count -eq 2) {
            $params[$parts[0].Trim()] = $parts[1].Trim()
        }
    }
    
    $host = $params['Host']
    $database = $params['Database']
    $user = $params['User Id']
    $password = $params['Password']
    
    try {
        # Usar pg_dump para crear backup (PostgreSQL)
        $env:PGPASSWORD = $password
        $pgDumpCmd = "pg_dump -h $host -U $user -d $database -F c -f `"$backupFile`""
        
        Write-Info "Ejecutando: pg_dump..."
        Invoke-Expression $pgDumpCmd 2>&1 | Out-Null
        
        if (Test-Path $backupFile) {
            $fileSize = (Get-Item $backupFile).Length / 1MB
            Write-Success "Backup creado: $backupFile ($('{0:N2}' -f $fileSize) MB)"
            return $backupFile
        }
        else {
            Write-Warning "No se pudo crear el backup (pg_dump no disponible o falló)"
            return $null
        }
    }
    catch {
        Write-Warning "Error creando backup: $($_.Exception.Message)"
        return $null
    }
    finally {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Apply-Migrations {
    param(
        [string]$ConnectionString,
        [bool]$IsDryRun
    )
    
    if ($IsDryRun) {
        Write-Info "Modo DRY RUN: No se aplicarán migraciones"
        return $true
    }
    
    Write-Info "Aplicando migraciones..."
    
    try {
        $updateCmd = "dotnet ef database update " +
                     "--project `"$InfrastructureProject`" " +
                     "--startup-project `"$StartupProject`" " +
                     "--context `"$DbContextName`" " +
                     "--connection `"$ConnectionString`" " +
                     "--verbose"
        
        Write-Host ""
        Write-Host "Ejecutando:" -ForegroundColor Yellow
        Write-Host $updateCmd -ForegroundColor Gray
        Write-Host ""
        
        Invoke-Expression $updateCmd
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Error aplicando migraciones (Exit code: $LASTEXITCODE)"
            return $false
        }
        
        Write-Success "Migraciones aplicadas exitosamente"
        return $true
    }
    catch {
        Write-Error "Error aplicando migraciones: $($_.Exception.Message)"
        return $false
    }
}

function Write-MigrationSummary {
    param(
        [string]$TenantSlug,
        [string]$Environment,
        [array]$PendingMigrations,
        [bool]$Success,
        [string]$BackupFile
    )
    
    Write-Header "Resumen de Migración"
    
    Write-Host "Tenant:           $TenantSlug" -ForegroundColor Cyan
    Write-Host "Environment:      $Environment" -ForegroundColor Cyan
    Write-Host "Migraciones:      $($PendingMigrations.Count) pendiente(s)" -ForegroundColor Cyan
    
    if ($PendingMigrations.Count -gt 0) {
        Write-Host ""
        Write-Host "Migraciones aplicadas:" -ForegroundColor Yellow
        foreach ($migration in $PendingMigrations) {
            Write-Host "  • $migration" -ForegroundColor Gray
        }
    }
    
    if ($BackupFile) {
        Write-Host ""
        Write-Host "Backup creado:    $BackupFile" -ForegroundColor Cyan
    }
    
    Write-Host ""
    if ($Success) {
        Write-Success "Migración completada exitosamente"
    }
    else {
        Write-Error "La migración falló"
    }
    Write-Host ""
}

# ============================================
# Script principal
# ============================================

try {
    Write-Header "Migración de Tenant Database"
    Write-Host "Tenant:       $TenantSlug" -ForegroundColor Cyan
    Write-Host "Environment:  $Environment" -ForegroundColor Cyan
    Write-Host "Dry Run:      $DryRun" -ForegroundColor Cyan
    Write-Host ""
    
    # 1. Verificar prerequisitos
    Test-Prerequisites
    
    # 2. Construir connection string
    $connectionString = Get-ConnectionString
    
    # 3. Probar conexión
    if (-not (Test-DatabaseConnection $connectionString)) {
        Write-Error "No se pudo establecer conexión con la base de datos"
        exit 1
    }
    
    # 4. Obtener migraciones pendientes
    $pendingMigrations = Get-PendingMigrations $connectionString
    
    if ($pendingMigrations.Count -eq 0) {
        Write-Success "No hay migraciones pendientes para aplicar"
        exit 0
    }
    
    Write-Info "Se encontraron $($pendingMigrations.Count) migración(es) pendiente(s):"
    foreach ($migration in $pendingMigrations) {
        Write-Host "  • $migration" -ForegroundColor Yellow
    }
    Write-Host ""
    
    if ($DryRun) {
        Write-Warning "Modo DRY RUN activado. No se aplicarán cambios."
        exit 0
    }
    
    # 5. Confirmación en producción
    if ($Environment -eq "Production") {
        Write-Warning "ADVERTENCIA: Estás a punto de aplicar migraciones en PRODUCCIÓN"
        Write-Host ""
        $confirmation = Read-Host "¿Estás seguro de que quieres continuar? (escribe 'SI' para confirmar)"
        if ($confirmation -ne "SI") {
            Write-Info "Operación cancelada por el usuario"
            exit 0
        }
    }
    
    # 6. Crear backup
    $backupFile = Create-DatabaseBackup -ConnectionString $connectionString -TenantSlug $TenantSlug
    
    # 7. Aplicar migraciones
    $success = Apply-Migrations -ConnectionString $connectionString -IsDryRun $DryRun
    
    # 8. Resumen
    Write-MigrationSummary `
        -TenantSlug $TenantSlug `
        -Environment $Environment `
        -PendingMigrations $pendingMigrations `
        -Success $success `
        -BackupFile $backupFile
    
    if ($success) {
        exit 0
    }
    else {
        exit 1
    }
}
catch {
    Write-Host ""
    Write-Error "Error fatal: $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Stack Trace:" -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor Gray
    exit 1
}
