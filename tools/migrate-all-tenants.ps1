<#
.SYNOPSIS
    Aplica migraciones a TODOS los tenants activos.

.DESCRIPTION
    Este script consulta la base de datos Admin para obtener todos los tenants activos
    y aplica las migraciones de Tenant DB a cada uno.

.PARAMETER Environment
    El entorno donde se ejecutará (Development, Staging, Production). Default: Development.

.PARAMETER AdminConnectionString
    Connection string de la base de datos Admin. Si no se proporciona, se usa el del entorno.

.PARAMETER TenantConnectionStringTemplate
    Template del connection string para los tenants.

.PARAMETER SkipBackup
    Si se especifica, no se crean backups antes de aplicar las migraciones.

.PARAMETER DryRun
    Si se especifica, solo muestra los tenants que se migrarían sin aplicar cambios.

.PARAMETER ContinueOnError
    Si se especifica, continúa con el siguiente tenant si uno falla.

.EXAMPLE
    .\migrate-all-tenants.ps1
    
.EXAMPLE
    .\migrate-all-tenants.ps1 -Environment "Production" -DryRun
    
.EXAMPLE
    .\migrate-all-tenants.ps1 -ContinueOnError

.NOTES
    Autor: eCommerce Team
    Fecha: Diciembre 2024
    Versión: 1.0.0
    Requiere: migrate-tenant.ps1, .NET 8 SDK
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment = "Development",

    [Parameter(Mandatory = $false)]
    [string]$AdminConnectionString,

    [Parameter(Mandatory = $false)]
    [string]$TenantConnectionStringTemplate,

    [Parameter(Mandatory = $false)]
    [switch]$SkipBackup,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [switch]$ContinueOnError,

    [Parameter(Mandatory = $false)]
    [int]$MaxParallel = 1
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

# ============================================
# Configuración
# ============================================

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$MigrateTenantScript = Join-Path $ScriptRoot "migrate-tenant.ps1"

# Connection strings por entorno
$AdminConnectionStrings = @{
    "Development" = "Host=localhost;Database=ecommerce_admin;User Id=postgres;Password=postgres;TrustServerCertificate=true"
    "Staging"     = "Host=staging-db.example.com;Database=ecommerce_admin;User Id=api_user;Password=$($env:STAGING_DB_PASSWORD);TrustServerCertificate=true"
    "Production"  = "Host=prod-db.example.com;Database=ecommerce_admin;User Id=api_user;Password=$($env:PROD_DB_PASSWORD);TrustServerCertificate=true"
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

function Get-AdminConnectionString {
    $connectionString = $AdminConnectionString
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        $connectionString = $AdminConnectionStrings[$Environment]
    }
    
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw "No se encontró connection string de Admin para el entorno: $Environment"
    }
    
    return $connectionString
}

function Get-ActiveTenants {
    param([string]$AdminConnStr)
    
    Write-Info "Consultando tenants activos desde Admin DB..."
    
    try {
        # Extraer parámetros del connection string
        $params = @{}
        $AdminConnStr.Split(';') | ForEach-Object {
            $parts = $_.Split('=', 2)
            if ($parts.Count -eq 2) {
                $params[$parts[0].Trim()] = $parts[1].Trim()
            }
        }
        
        $host = $params['Host']
        $database = $params['Database']
        $user = $params['User Id']
        $password = $params['Password']
        
        # Query para obtener tenants activos
        $query = "SELECT slug, name, plan FROM admin.tenants WHERE status = 'Active' ORDER BY slug;"
        
        # Ejecutar query con psql
        $env:PGPASSWORD = $password
        $result = & psql -h $host -U $user -d $database -t -A -F "," -c $query 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            throw "Error ejecutando query: $result"
        }
        
        # Parsear resultado
        $tenants = @()
        $result | ForEach-Object {
            if (![string]::IsNullOrWhiteSpace($_)) {
                $parts = $_.Split(',')
                if ($parts.Count -ge 2) {
                    $tenants += [PSCustomObject]@{
                        Slug = $parts[0]
                        Name = $parts[1]
                        Plan = if ($parts.Count -ge 3) { $parts[2] } else { "Unknown" }
                    }
                }
            }
        }
        
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        
        Write-Success "Se encontraron $($tenants.Count) tenant(s) activo(s)"
        return $tenants
    }
    catch {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        throw "Error consultando tenants: $($_.Exception.Message)"
    }
}

function Invoke-TenantMigration {
    param(
        [PSCustomObject]$Tenant,
        [string]$Environment,
        [string]$ConnectionStringTemplate,
        [bool]$SkipBackup,
        [bool]$DryRun
    )
    
    Write-Header "Migrando Tenant: $($Tenant.Slug)"
    
    $args = @(
        "-TenantSlug", $Tenant.Slug,
        "-Environment", $Environment
    )
    
    if ($ConnectionStringTemplate) {
        $args += @("-ConnectionStringTemplate", $ConnectionStringTemplate)
    }
    
    if ($SkipBackup) {
        $args += "-SkipBackup"
    }
    
    if ($DryRun) {
        $args += "-DryRun"
    }
    
    try {
        & $MigrateTenantScript @args
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Migración exitosa para tenant: $($Tenant.Slug)"
            return $true
        }
        else {
            Write-Error "Migración falló para tenant: $($Tenant.Slug) (Exit code: $LASTEXITCODE)"
            return $false
        }
    }
    catch {
        Write-Error "Error migrando tenant $($Tenant.Slug): $($_.Exception.Message)"
        return $false
    }
}

function Write-MigrationReport {
    param(
        [array]$Results,
        [int]$TotalTenants
    )
    
    Write-Header "Reporte de Migración"
    
    $successful = ($Results | Where-Object { $_.Success }).Count
    $failed = ($Results | Where-Object { !$_.Success }).Count
    
    Write-Host "Total de tenants:     $TotalTenants" -ForegroundColor Cyan
    Write-Host "Migraciones exitosas: $successful" -ForegroundColor Green
    Write-Host "Migraciones fallidas: $failed" -ForegroundColor Red
    Write-Host ""
    
    if ($failed -gt 0) {
        Write-Host "Tenants fallidos:" -ForegroundColor Red
        $Results | Where-Object { !$_.Success } | ForEach-Object {
            Write-Host "  ? $($_.Tenant.Slug) - $($_.Tenant.Name)" -ForegroundColor Red
        }
        Write-Host ""
    }
    
    if ($successful -gt 0) {
        Write-Host "Tenants exitosos:" -ForegroundColor Green
        $Results | Where-Object { $_.Success } | ForEach-Object {
            Write-Host "  ? $($_.Tenant.Slug) - $($_.Tenant.Name)" -ForegroundColor Green
        }
        Write-Host ""
    }
    
    $successRate = if ($TotalTenants -gt 0) { ($successful / $TotalTenants) * 100 } else { 0 }
    Write-Host "Tasa de éxito: $("{0:N2}" -f $successRate)%" -ForegroundColor $(if ($successRate -eq 100) { "Green" } elseif ($successRate -gt 75) { "Yellow" } else { "Red" })
    Write-Host ""
}

# ============================================
# Script principal
# ============================================

try {
    Write-Header "Migración de Todos los Tenants"
    Write-Host "Environment:  $Environment" -ForegroundColor Cyan
    Write-Host "Dry Run:      $DryRun" -ForegroundColor Cyan
    Write-Host "Skip Backup:  $SkipBackup" -ForegroundColor Cyan
    Write-Host ""
    
    # Verificar que existe el script de migración individual
    if (-not (Test-Path $MigrateTenantScript)) {
        Write-Error "No se encontró el script de migración: $MigrateTenantScript"
        exit 1
    }
    
    # Obtener connection string de Admin
    $adminConnStr = Get-AdminConnectionString
    
    # Obtener lista de tenants activos
    $tenants = Get-ActiveTenants -AdminConnStr $adminConnStr
    
    if ($tenants.Count -eq 0) {
        Write-Warning "No se encontraron tenants activos para migrar"
        exit 0
    }
    
    Write-Host ""
    Write-Host "Tenants a migrar:" -ForegroundColor Yellow
    $tenants | ForEach-Object {
        Write-Host "  • $($_.Slug) - $($_.Name) ($($_.Plan))" -ForegroundColor Gray
    }
    Write-Host ""
    
    if ($DryRun) {
        Write-Warning "Modo DRY RUN activado. No se aplicarán cambios."
        exit 0
    }
    
    # Confirmación en producción
    if ($Environment -eq "Production") {
        Write-Warning "ADVERTENCIA: Estás a punto de aplicar migraciones a TODOS los tenants en PRODUCCIÓN"
        Write-Host ""
        $confirmation = Read-Host "¿Estás seguro de que quieres continuar? (escribe 'SI ESTOY SEGURO' para confirmar)"
        if ($confirmation -ne "SI ESTOY SEGURO") {
            Write-Info "Operación cancelada por el usuario"
            exit 0
        }
    }
    
    # Aplicar migraciones a cada tenant
    $results = @()
    $currentTenant = 0
    
    foreach ($tenant in $tenants) {
        $currentTenant++
        Write-Host ""
        Write-Host "[$currentTenant/$($tenants.Count)] Procesando: $($tenant.Slug)" -ForegroundColor Cyan
        
        $success = Invoke-TenantMigration `
            -Tenant $tenant `
            -Environment $Environment `
            -ConnectionStringTemplate $TenantConnectionStringTemplate `
            -SkipBackup $SkipBackup `
            -DryRun $DryRun
        
        $results += [PSCustomObject]@{
            Tenant  = $tenant
            Success = $success
        }
        
        if (!$success -and !$ContinueOnError) {
            Write-Error "Migración falló para tenant: $($tenant.Slug). Deteniendo proceso."
            break
        }
        
        # Pequeña pausa entre migraciones
        Start-Sleep -Seconds 2
    }
    
    # Reporte final
    Write-MigrationReport -Results $results -TotalTenants $tenants.Count
    
    # Exit code basado en resultados
    $failedCount = ($results | Where-Object { !$_.Success }).Count
    if ($failedCount -gt 0) {
        exit 1
    }
    else {
        exit 0
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
