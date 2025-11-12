# Endpoints de Aprovisionamiento de Tenants - Guía de Uso

## Descripción General

Sistema completo de aprovisionamiento de tenants con 3 endpoints:
1. **POST /provision/tenants/init** - Inicializa aprovisionamiento
2. **POST /provision/tenants/confirm** - Confirma y encola para procesamiento
3. **GET /provision/tenants/{provisioningId}/status** - Consulta estado

## Flujo de Aprovisionamiento

```
1. Cliente ? POST /provision/tenants/init
   ?
2. API valida datos y crea tenant (Status: PENDING_VALIDATION)
   ?
3. API genera confirmToken (JWT válido 15 min)
   ?
4. Cliente ? POST /provision/tenants/confirm (con confirmToken)
   ?
5. API valida token y cambia status a QUEUED
   ?
6. BackgroundWorker procesa aprovisionamiento
   ?? Paso 1: CreateDatabase
   ?? Paso 2: ApplyMigrations
   ?? Paso 3: SeedData
   ?
7. Tenant activo (Status: Active)
```

## ?? Ejemplos cURL

### 1. Inicializar Aprovisionamiento

```bash
curl -X POST "http://localhost:5000/provision/tenants/init" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Acme Corporation",
    "slug": "acme",
    "plan": "Premium"
  }'
```

**Response Exitoso (200 OK):**
```json
{
  "provisioningId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "confirmToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "next": "/provision/tenants/confirm",
  "message": "Provisioning initialized. Use the confirmation token within 15 minutes to proceed."
}
```

**Response Error - Slug Duplicado (409 Conflict):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Slug Already Exists",
  "status": 409,
  "detail": "A tenant with slug 'acme' already exists"
}
```

**Response Error - Plan Inválido (400 Bad Request):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Invalid Plan",
  "status": 400,
  "detail": "Plan 'InvalidPlan' is not allowed. Allowed plans: Basic, Premium, Enterprise"
}
```

### 2. Confirmar Aprovisionamiento

```bash
# Guardar el token de la respuesta anterior
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X POST "http://localhost:5000/provision/tenants/confirm" \
  -H "Authorization: Bearer $TOKEN"
```

**Response Exitoso (200 OK):**
```json
{
  "provisioningId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "QUEUED",
  "message": "Provisioning confirmed and queued for processing",
  "statusEndpoint": "/provision/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/status"
}
```

**Response Error - Token Inválido/Expirado (401 Unauthorized):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Invalid Token",
  "status": 401,
  "detail": "The confirmation token is invalid or has expired"
}
```

**Response Error - Sin Authorization Header (401 Unauthorized):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Missing Authorization",
  "status": 401,
  "detail": "Authorization header with Bearer token is required"
}
```

### 3. Consultar Estado del Aprovisionamiento

```bash
PROVISIONING_ID="3fa85f64-5717-4562-b3fc-2c963f66afa6"

curl -X GET "http://localhost:5000/provision/tenants/$PROVISIONING_ID/status"
```

**Response - En Proceso (200 OK):**
```json
{
  "status": "Provisioning",
  "tenantSlug": null,
  "dbName": null,
  "steps": [
    {
      "step": "Init",
      "status": "Success",
      "startedAt": "2025-01-10T10:00:00Z",
      "completedAt": "2025-01-10T10:00:01Z",
      "log": "Provisioning initialized, waiting for confirmation",
      "errorMessage": null
    },
    {
      "step": "CreateDatabase",
      "status": "InProgress",
      "startedAt": "2025-01-10T10:05:00Z",
      "completedAt": null,
      "log": null,
      "errorMessage": null
    }
  ]
}
```

**Response - Completado (200 OK):**
```json
{
  "status": "Active",
  "tenantSlug": "acme",
  "dbName": "ecom_tenant_acme",
  "steps": [
    {
      "step": "Init",
      "status": "Success",
      "startedAt": "2025-01-10T10:00:00Z",
      "completedAt": "2025-01-10T10:00:01Z",
      "log": "Provisioning initialized, waiting for confirmation",
      "errorMessage": null
    },
    {
      "step": "CreateDatabase",
      "status": "Success",
      "startedAt": "2025-01-10T10:05:00Z",
      "completedAt": "2025-01-10T10:05:10Z",
      "log": "Database ecom_tenant_acme created successfully",
      "errorMessage": null
    },
    {
      "step": "ApplyMigrations",
      "status": "Success",
      "startedAt": "2025-01-10T10:05:11Z",
      "completedAt": "2025-01-10T10:05:25Z",
      "log": "Migrations applied successfully",
      "errorMessage": null
    },
    {
      "step": "SeedData",
      "status": "Success",
      "startedAt": "2025-01-10T10:05:26Z",
      "completedAt": "2025-01-10T10:05:30Z",
      "log": "Demo data seeded successfully",
      "errorMessage": null
    }
  ]
}
```

**Response - Error (200 OK con status Failed):**
```json
{
  "status": "Failed",
  "tenantSlug": null,
  "dbName": null,
  "steps": [
    {
      "step": "Init",
      "status": "Success",
      "startedAt": "2025-01-10T10:00:00Z",
      "completedAt": "2025-01-10T10:00:01Z",
      "log": "Provisioning initialized, waiting for confirmation",
      "errorMessage": null
    },
    {
      "step": "CreateDatabase",
      "status": "Failed",
      "startedAt": "2025-01-10T10:05:00Z",
      "completedAt": "2025-01-10T10:05:05Z",
      "log": null,
      "errorMessage": "Failed to connect to database server"
    }
  ]
}
```

## ?? Script Completo de Aprovisionamiento

```bash
#!/bin/bash

# Configuración
API_URL="http://localhost:5000"
TENANT_NAME="Acme Corporation"
TENANT_SLUG="acme"
TENANT_PLAN="Premium"

echo "=== Iniciando Aprovisionamiento de Tenant ==="
echo "Nombre: $TENANT_NAME"
echo "Slug: $TENANT_SLUG"
echo "Plan: $TENANT_PLAN"
echo ""

# 1. Inicializar
echo "1. Inicializando aprovisionamiento..."
INIT_RESPONSE=$(curl -s -X POST "$API_URL/provision/tenants/init" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"$TENANT_NAME\",
    \"slug\": \"$TENANT_SLUG\",
    \"plan\": \"$TENANT_PLAN\"
  }")

echo "Response:"
echo "$INIT_RESPONSE" | jq .
echo ""

# Extraer token y provisioningId
CONFIRM_TOKEN=$(echo "$INIT_RESPONSE" | jq -r '.confirmToken')
PROVISIONING_ID=$(echo "$INIT_RESPONSE" | jq -r '.provisioningId')

if [ "$CONFIRM_TOKEN" == "null" ] || [ "$PROVISIONING_ID" == "null" ]; then
  echo "Error: No se pudo inicializar el aprovisionamiento"
  exit 1
fi

echo "Provisioning ID: $PROVISIONING_ID"
echo "Token obtenido (válido por 15 minutos)"
echo ""

# 2. Confirmar
echo "2. Confirmando aprovisionamiento..."
CONFIRM_RESPONSE=$(curl -s -X POST "$API_URL/provision/tenants/confirm" \
  -H "Authorization: Bearer $CONFIRM_TOKEN")

echo "Response:"
echo "$CONFIRM_RESPONSE" | jq .
echo ""

# 3. Monitorear estado
echo "3. Monitoreando estado del aprovisionamiento..."
while true; do
  STATUS_RESPONSE=$(curl -s -X GET "$API_URL/provision/tenants/$PROVISIONING_ID/status")
  STATUS=$(echo "$STATUS_RESPONSE" | jq -r '.status')
  
  echo "Estado actual: $STATUS"
  
  if [ "$STATUS" == "Active" ]; then
    echo ""
    echo "? Aprovisionamiento completado exitosamente!"
    echo ""
    echo "Detalles del tenant:"
    echo "$STATUS_RESPONSE" | jq .
    break
  elif [ "$STATUS" == "Failed" ]; then
    echo ""
    echo "? Aprovisionamiento falló"
    echo ""
    echo "Detalles del error:"
    echo "$STATUS_RESPONSE" | jq .
    exit 1
  fi
  
  sleep 3
done
```

## ?? Validaciones Implementadas

### Init Endpoint
- ? Nombre: mínimo 3 caracteres, máximo 200
- ? Slug: formato `^[a-z0-9-]+$`, mínimo 3, máximo 50
- ? Slug: verificación de unicidad
- ? Plan: debe ser uno de: Basic, Premium, Enterprise
- ?? TODO: Rate limiting
- ?? TODO: Captcha/HMAC validation

### Confirm Endpoint
- ? Token JWT requerido en header Authorization
- ? Validación de firma del token
- ? Validación de expiración (15 minutos)
- ? Validación de tipo de token (confirm_provisioning)
- ? Validación de estado del tenant (debe ser PENDING_VALIDATION)

### Status Endpoint
- ? Validación de existencia del tenant
- ? Retorna historial completo de pasos
- ? Información sensible solo se retorna cuando status = Active

## ?? Seguridad

### Tokens de Confirmación
- Algoritmo: HMAC-SHA256
- Duración: 15 minutos
- Claims incluidos:
  - `sub`: provisioningId (Guid)
  - `slug`: slug del tenant
  - `type`: "confirm_provisioning"
  - `jti`: GUID único por token

### Estados del Tenant
```
PENDING_VALIDATION ? QUEUED ? Provisioning ? Active
                                          ? Failed
```

## ?? Logging

Todos los endpoints registran eventos con Serilog:

```
Information: Tenant provisioning initialized. TenantId: {TenantId}, Slug: {Slug}, Plan: {Plan}
Information: Tenant provisioning confirmed and queued. TenantId: {TenantId}, Slug: {Slug}
Information: Processing provisioning for tenant {TenantId}
Information: Creating database {DbName} for tenant {TenantId}
Information: Database {DbName} created for tenant {TenantId}
Information: Applying migrations to database {DbName} for tenant {TenantId}
Information: Migrations applied to {DbName} for tenant {TenantId}
Information: Seeding data for tenant {TenantId} in database {DbName}
Information: Data seeded for tenant {TenantId}
Information: Provisioning completed successfully for tenant {TenantId}

Error: Error provisioning tenant {TenantId} ({Slug}): {ErrorMessage}
Error: Unhandled error processing tenant {TenantId}: {ErrorMessage}
```

## ?? Próximos Pasos (TODO)

1. **Rate Limiting**
   - Implementar límite de requests por IP
   - Límite de tenants por usuario/email

2. **Captcha/HMAC**
   - Validación de captcha en init endpoint
   - HMAC signature para prevenir abuse

3. **Migraciones y Seed Reales**
   - Implementar aplicación de migraciones EF Core
   - Seed de datos demo del catálogo
   - Seed de configuraciones por defecto

4. **Notificaciones**
   - Email cuando tenant esté listo
   - Webhook para notificar cambios de estado

5. **Rollback**
   - Implementar rollback automático si falla algún paso
   - Cleanup de recursos parcialmente creados

## ?? Soporte

Para más información, consultar:
- Swagger UI: http://localhost:5000/swagger
- Logs: Ver archivo de log configurado en Serilog
- Admin DB: Consultar tablas `admin.Tenants` y `admin.TenantProvisionings`
