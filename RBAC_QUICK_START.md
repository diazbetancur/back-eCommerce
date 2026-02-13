# 🚀 Quick Start Guide - RBAC Implementation

## 📚 Documentación Completa

Este proyecto incluye tres archivos de documentación para la implementación de RBAC:

1. **RBAC_API_DOCUMENTATION.md** - Documentación completa de todos los endpoints
2. **RBAC_FRONTEND_INTEGRATION.ts** - Código TypeScript/JavaScript listo para usar
3. **dev/rbac-endpoints.http** - Ejemplos de requests HTTP para testing

---

## ✅ Estado Actual

### Servicios Backend (100% Completo)

- ✅ `IUserManagementService` - Gestión completa de usuarios
- ✅ `IRoleService` - CRUD de roles y permisos
- ✅ `IPermissionService` - Verificación de permisos
- ✅ Todos registrados en `Program.cs`

### Endpoints API (100% Completo)

**Usuarios** (`/admin/users`)
- ✅ GET - Listar usuarios (con filtros y paginación)
- ✅ GET /{id} - Detalle de usuario
- ✅ POST - Crear usuario
- ✅ PUT /{id}/roles - Actualizar roles
- ✅ PATCH /{id}/status - Activar/desactivar
- ✅ DELETE /{id} - Eliminar (soft delete)

**Roles** (`/admin/roles`)
- ✅ GET - Listar roles
- ✅ GET /{id} - Detalle de rol
- ✅ POST - Crear rol
- ✅ PUT /{id} - Actualizar rol
- ✅ DELETE /{id} - Eliminar rol

**Permisos** (`/admin/roles`)
- ✅ GET /available-modules - Catálogo de módulos
- ✅ GET /{id}/permissions - Permisos del rol
- ✅ PUT /{id}/permissions - Actualizar permisos

---

## 🔐 Autenticación Requerida

Todos los endpoints requieren dos headers:

```http
Authorization: Bearer {jwt-token}
X-Tenant-Slug: {tenant-slug}
```

---

## 📋 Módulos Disponibles

| Código | Nombre | Permisos |
|--------|--------|----------|
| `inventory` | Gestión de Inventario | view, create, update, delete |
| `sales` | Gestión de Ventas | view, create, update, delete |
| `customers` | Gestión de Clientes | view, create, update, delete |
| `users` | Gestión de Usuarios y Roles | view, create, update, delete |
| `settings` | Configuración de Tienda | view, update |
| `marketing` | Marketing y Promociones | view, create, update, delete |
| `reports` | Reportes y Análisis | view |

---

## 🎯 Casos de Uso Comunes

### 1. Crear un rol y asignar permisos

```typescript
// 1. Crear el rol
const role = await roleService.createRole({
  name: "Store Manager",
  description: "Full store management"
});

// 2. Asignar permisos
await permissionService.updateRolePermissions(role.id, {
  permissions: [
    {
      moduleCode: "inventory",
      canView: true,
      canCreate: true,
      canUpdate: true,
      canDelete: true
    },
    {
      moduleCode: "sales",
      canView: true,
      canCreate: true,
      canUpdate: true,
      canDelete: false
    }
  ]
});
```

### 2. Crear usuario con roles específicos

```typescript
const user = await userService.createUser({
  email: "manager@example.com",
  firstName: "Store",
  lastName: "Manager",
  password: "SecurePass123!",
  roleIds: [roleId1, roleId2]
});
```

### 3. Actualizar roles de un usuario

```typescript
await userService.updateUserRoles(userId, {
  roleIds: [newRoleId1, newRoleId2]
});
```

### 4. Buscar usuarios por email/nombre

```typescript
const users = await userService.getUsers({
  search: "john",
  isActive: true,
  page: 1,
  pageSize: 10
});
```

---

## 🛡️ Características de Seguridad

### Protección contra Auto-Lockout
❌ No puedes remover tu propio rol de administrador  
✅ El sistema previene este escenario automáticamente

### Roles del Sistema
- `SuperAdmin` - No se puede eliminar ni renombrar
- `Customer` - No se puede eliminar ni renombrar

### Soft Delete
Los usuarios eliminados se marcan como borrados pero permanecen en la BD para auditoría.

### Permisos Acumulativos
Si un usuario tiene múltiples roles, los permisos se combinan (el más permisivo gana).

---

## 📱 Integración Frontend

### React

```typescript
import { RBACApiClient, UserService, RoleService } from './rbac-service';

const api = new RBACApiClient(API_URL, token, tenantSlug);
const userService = new UserService(api);

// Crear usuario
const newUser = await userService.createUser({...});
```

### Angular

```typescript
import { HttpClient } from '@angular/common/http';

export class RBACService {
  constructor(private http: HttpClient) {}
  
  getUsers() {
    return this.http.get('/admin/users', {
      headers: {
        'Authorization': `Bearer ${token}`,
        'X-Tenant-Slug': tenantSlug
      }
    });
  }
}
```

### Vue/Nuxt

```typescript
// composables/useRBAC.ts
export const useRBAC = () => {
  const getUsers = async () => {
    return await $fetch('/admin/users', {
      headers: {
        'Authorization': `Bearer ${token}`,
        'X-Tenant-Slug': tenantSlug
      }
    });
  };
  
  return { getUsers };
};
```

---

## 🧪 Testing

### Usar el archivo HTTP

1. Abre `dev/rbac-endpoints.http`
2. Instala la extensión "REST Client" en VS Code
3. Actualiza las variables:
   ```
   @token = tu-jwt-token
   @tenantSlug = tu-tenant-slug
   ```
4. Click en "Send Request" sobre cada endpoint

### Postman

Importa los ejemplos del archivo `RBAC_API_DOCUMENTATION.md` y crea requests en Postman.

---

## ⚠️ Errores Comunes

| Código | Error | Solución |
|--------|-------|----------|
| 401 | Unauthorized | Verificar token JWT válido |
| 403 | Forbidden | Usuario sin permisos para el módulo |
| 404 | Not Found | ID de usuario/rol no existe |
| 409 | Conflict | Nombre de rol duplicado o rol con usuarios asignados |
| 400 | Bad Request | Validación de datos fallida |

---

## 📞 Endpoints Principales

### Base URL
```
https://api-ecommerce-d9fxeccbeeehdjd3.eastus-01.azurewebsites.net
```

### Endpoints
- `GET /admin/users` - Listar usuarios
- `POST /admin/users` - Crear usuario
- `GET /admin/roles` - Listar roles
- `POST /admin/roles` - Crear rol
- `GET /admin/roles/available-modules` - Módulos disponibles
- `PUT /admin/roles/{id}/permissions` - Actualizar permisos

---

## 📊 Flujo Recomendado de Implementación Frontend

1. **Crear servicios API** usando `RBAC_FRONTEND_INTEGRATION.ts`
2. **Implementar pantalla de Roles**
   - Listar roles
   - Crear/editar rol
   - Asignar permisos por módulo
3. **Implementar pantalla de Usuarios**
   - Listar usuarios con búsqueda
   - Crear usuario
   - Asignar/modificar roles
   - Activar/desactivar usuarios
4. **Agregar guards de permisos**
   - Verificar permisos antes de mostrar opciones
   - Deshabilitar botones según permisos
5. **Testing completo**
   - Probar con diferentes roles
   - Verificar protección de auto-lockout

---

## 🎨 UI/UX Recomendaciones

### Pantalla de Roles
- Tabla con nombre, descripción, usuarios asignados
- Modal para crear/editar rol
- Grid de módulos con checkboxes para permisos (view, create, update, delete)
- Badge para marcar roles del sistema

### Pantalla de Usuarios
- Tabla con nombre, email, roles, estado (activo/inactivo)
- Búsqueda en tiempo real
- Filtros: rol, estado activo
- Modal para crear usuario con selector múltiple de roles
- Botón toggle para activar/desactivar
- Confirmación antes de eliminar

### Permisos Visuales
- Iconos para cada tipo de permiso (👁️ view, ➕ create, ✏️ update, 🗑️ delete)
- Indicador visual cuando usuario no tiene permisos
- Deshabilitar acciones sin permiso

---

## ✨ Próximos Pasos

1. ✅ **Backend listo** - Todos los servicios y endpoints implementados
2. 🔄 **Frontend** - Implementar pantallas usando la documentación
3. 🧪 **Testing** - Probar todos los escenarios con diferentes roles
4. 🚀 **Deploy** - Verificar configuración en producción

---

**Última actualización:** 12 de febrero de 2026  
**Estado:** ✅ Listo para implementación en frontend
