# 🔧 Solución: Error 401/403 en Endpoints de Loyalty e Inventory

## 🎯 Problema

Si tu tenant fue creado **antes** de que existieran los módulos de `loyalty` e `inventory`, el administrador no tendrá permisos para usar esos endpoints, resultando en errores:
- **401 Unauthorized**: No autenticado o token inválido
- **403 Forbidden**: No tiene permisos para el módulo

## ✅ Solución Rápida: Endpoint Temporal

He creado un endpoint temporal que actualiza automáticamente los módulos y permisos faltantes.

### Paso 1: Reiniciar el servidor

```bash
cd /Users/diazbetancur/Proyectos/Generics/Back/back-eCommerce/Api-eCommerce
dotnet run
```

### Paso 2: Ejecutar el Fix

Hacer una petición POST al endpoint de fix:

**Request:**
```http
POST http://localhost:5093/admin/fix-modules
Authorization: Bearer {tu-token}
X-Tenant-Slug: {tu-tenant-slug}
```

**Ejemplo con cURL:**
```bash
curl -X POST http://localhost:5093/admin/fix-modules \
  -H "Authorization: Bearer TU_TOKEN_AQUI" \
  -H "X-Tenant-Slug: test" \
  -H "Content-Type: application/json"
```

**Ejemplo con Thunder Client / Postman:**
```
Method: POST
URL: http://localhost:5093/admin/fix-modules
Headers:
  - Authorization: Bearer {token}
  - X-Tenant-Slug: test
  - Content-Type: application/json
```

### Paso 3: Verificar la Respuesta

**Response 200 (Éxito):**
```json
{
  "message": "Módulos y permisos actualizados correctamente",
  "modulesCreated": 2,
  "permissionsCreated": 3,
  "details": [
    "✅ Módulo 'loyalty' creado",
    "✅ Módulo 'inventory' creado",
    "✅ Permisos de SuperAdmin para 'loyalty' creados",
    "✅ Permisos de SuperAdmin para 'inventory' creados",
    "✅ Permisos de Customer para 'loyalty' creados"
  ]
}
```

Si los módulos ya existían:
```json
{
  "message": "Módulos y permisos actualizados correctamente",
  "modulesCreated": 0,
  "permissionsCreated": 0,
  "details": [
    "⏭️ Módulo 'loyalty' ya existe",
    "⏭️ Módulo 'inventory' ya existe",
    "⏭️ No se requieren permisos nuevos"
  ]
}
```

### Paso 4: Probar Endpoints de Loyalty

Ahora deberías poder usar los endpoints sin errores:

```http
GET http://localhost:5093/admin/loyalty/configuration
Authorization: Bearer {token}
X-Tenant-Slug: test
```

```http
PUT http://localhost:5093/admin/loyalty/configuration
Authorization: Bearer {token}
X-Tenant-Slug: test
Content-Type: application/json

{
  "pointsPerCurrency": 1,
  "currencyPerPoint": 0.01,
  "minPointsToRedeem": 100
}
```

---

## 🔍 Verificación Manual de Permisos

Si quieres verificar los permisos manualmente:

### 1. Verificar módulos existentes

```sql
-- Conectarse a la base de datos del tenant
SELECT * FROM "Modules" WHERE "Code" IN ('loyalty', 'inventory');
```

### 2. Verificar permisos del SuperAdmin

```sql
-- Ver permisos del rol SuperAdmin
SELECT 
    r."Name" AS "Role",
    m."Code" AS "Module",
    rmp."CanView",
    rmp."CanCreate",
    rmp."CanUpdate",
    rmp."CanDelete"
FROM "RoleModulePermissions" rmp
JOIN "Roles" r ON rmp."RoleId" = r."Id"
JOIN "Modules" m ON rmp."ModuleId" = m."Id"
WHERE r."Name" = 'SuperAdmin' 
  AND m."Code" IN ('loyalty', 'inventory');
```

**Resultado esperado:**
| Role | Module | CanView | CanCreate | CanUpdate | CanDelete |
|------|--------|---------|-----------|-----------|-----------|
| SuperAdmin | loyalty | true | true | true | true |
| SuperAdmin | inventory | true | true | true | true |

---

## 📝 Notas Importantes

### ⚠️ Este endpoint es TEMPORAL

El endpoint `/admin/fix-modules` es una solución temporal para tenants existentes. **Nuevos tenants** creados después del commit tendrán los módulos automáticamente.

### 🔐 Requiere Permisos

El endpoint requiere:
- Usuario autenticado (JWT token)
- Header `X-Tenant-Slug`
- Permiso `permissions:update` (solo SuperAdmin)

### 🗑️ Eliminar después

Una vez que todos los tenants hayan sido actualizados, puedes eliminar este endpoint del código.

---

## 🐛 Troubleshooting

### Error: "401 Unauthorized"

**Causa**: Token inválido o expirado

**Solución**:
1. Hacer login nuevamente para obtener un token fresco
2. Verificar que el header `Authorization: Bearer {token}` esté correcto

```http
POST http://localhost:5093/tenant-auth/login
Content-Type: application/json
X-Tenant-Slug: test

{
  "email": "admin@test",
  "password": "TenantAdmin123!"
}
```

### Error: "403 Forbidden" al ejecutar fix-modules

**Causa**: El usuario no tiene permiso `permissions:update`

**Solución**: Solo el SuperAdmin puede ejecutar este endpoint. Asegúrate de estar usando las credenciales del admin del tenant:
- Email: `admin@{tenant-slug}`
- Password: `TenantAdmin123!`

### Error: "Tenant not found"

**Causa**: Header `X-Tenant-Slug` faltante o incorrecto

**Solución**: Verificar que el header esté presente y el slug sea correcto:
```http
X-Tenant-Slug: test
```

### Los módulos se crean pero sigo sin permisos

**Solución**: Cerrar sesión y volver a iniciar sesión. El JWT token se genera con los permisos al momento del login, necesitas un token nuevo.

---

## 📚 Endpoints Documentados

Después de ejecutar el fix, revisa la documentación completa:

- **Loyalty API**: [docs/LOYALTY_API_GUIDE.md](./LOYALTY_API_GUIDE.md)
  - 15 endpoints (10 admin, 5 user)
  - Configuración de conversión
  - Gestión de recompensas y redenciones

- **Stores API**: [docs/FRONTEND_STORES_IMPLEMENTATION.md](./FRONTEND_STORES_IMPLEMENTATION.md)
  - 11 endpoints para stores/inventory
  - Gestión de tiendas
  - Stock multi-ubicación

---

## ✅ Resumen

1. ✅ Ejecutar `POST /admin/fix-modules` con token de SuperAdmin
2. ✅ Verificar respuesta exitosa
3. ✅ Hacer logout y login nuevamente (refrescar token)
4. ✅ Probar endpoints de loyalty e inventory
5. ✅ Todo debería funcionar correctamente

¿Tienes problemas? Revisa la sección de Troubleshooting arriba.
