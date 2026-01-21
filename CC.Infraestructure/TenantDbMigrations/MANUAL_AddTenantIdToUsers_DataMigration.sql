-- ============================================================================
-- SCRIPT DE MIGRACIÓN: Agregar TenantId a usuarios existentes
-- ============================================================================
-- Este script debe ejecutarse MANUALMENTE en cada tenant database después
-- de aplicar la migración EF Core.
--
-- IMPORTANTE: Reemplazar {TENANT_ID} con el ID real del tenant antes de ejecutar
-- ============================================================================

-- Paso 1: Verificar que la columna TenantId existe
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'Users' AND column_name = 'TenantId';

-- Paso 2: Obtener el TenantId desde la tabla admin (ejecutar en AdminDb primero)
-- Ejecutar en AdminDb:
-- SELECT "Id", "Slug", "Name" FROM admin."Tenants" WHERE "Slug" = 'tu-tenant-slug';

-- Paso 3: Actualizar usuarios existentes (reemplazar {TENANT_ID})
-- EJEMPLO:
-- UPDATE "Users" 
-- SET "TenantId" = 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d'
-- WHERE "TenantId" = '00000000-0000-0000-0000-000000000000'
--    OR "TenantId" IS NULL;

-- Paso 4: Verificar que todos los usuarios tienen TenantId
SELECT 
    COUNT(*) as total_users,
    COUNT("TenantId") as users_with_tenant,
    COUNT(*) - COUNT("TenantId") as users_without_tenant
FROM "Users";

-- Paso 5: Listar usuarios sin TenantId (si hay alguno)
SELECT "Id", "Email", "FirstName", "LastName", "CreatedAt"
FROM "Users"
WHERE "TenantId" IS NULL OR "TenantId" = '00000000-0000-0000-0000-000000000000';

-- ============================================================================
-- INSTRUCCIONES DE EJECUCIÓN:
-- ============================================================================
-- 1. Aplicar migración EF Core: dotnet ef database update
-- 2. Obtener TenantId desde AdminDb (query en Paso 2)
-- 3. Ejecutar UPDATE del Paso 3 con el TenantId correcto
-- 4. Verificar con queries del Paso 4 y 5
-- ============================================================================
