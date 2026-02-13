# 📚 API Documentation - RBAC (Roles, Users & Permissions)

## 🔐 Autenticación

**Todos los endpoints requieren:**
- Header `Authorization: Bearer {token}`
- Header `X-Tenant-Slug: {tenant-slug}`

## 📋 Tabla de Contenidos

1. [Gestión de Usuarios](#gestión-de-usuarios)
2. [Gestión de Roles](#gestión-de-roles)
3. [Gestión de Permisos](#gestión-de-permisos)
4. [DTOs y Modelos](#dtos-y-modelos)

---

# 👥 Gestión de Usuarios

Base URL: `/admin/users`

## 1. Listar Usuarios

```http
GET /admin/users
```

**Permisos requeridos:** `customers:view`

**Query Parameters:**
```typescript
{
  page?: number;           // Default: 1
  pageSize?: number;       // Default: 10
  search?: string;         // Buscar por nombre o email
  roleId?: string;         // Filtrar por rol (GUID)
  isActive?: boolean;      // Filtrar por estado activo
}
```

**Respuesta 200:**
```json
{
  "users": [
    {
      "id": "uuid",
      "email": "user@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "isActive": true,
      "roles": [
        {
          "id": "uuid",
          "name": "Admin",
          "isSystemRole": true
        }
      ],
      "createdAt": "2024-01-01T00:00:00Z",
      "lastLoginAt": "2024-01-15T10:30:00Z"
    }
  ],
  "totalUsers": 50,
  "currentPage": 1,
  "pageSize": 10,
  "totalPages": 5
}
```

---

## 2. Obtener Usuario por ID

```http
GET /admin/users/{id}
```

**Permisos requeridos:** `customers:view`

**Path Parameters:**
- `id` (uuid): ID del usuario

**Respuesta 200:**
```json
{
  "id": "uuid",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "isActive": true,
  "emailConfirmed": true,
  "roles": [
    {
      "id": "uuid",
      "name": "Admin",
      "description": "Full access administrator",
      "isSystemRole": true
    }
  ],
  "createdAt": "2024-01-01T00:00:00Z",
  "lastLoginAt": "2024-01-15T10:30:00Z",
  "lastModifiedAt": "2024-01-10T15:00:00Z"
}
```

**Respuesta 404:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "User not found"
}
```

---

## 3. Crear Usuario

```http
POST /admin/users
```

**Permisos requeridos:** `customers:create`

**Request Body:**
```json
{
  "email": "newuser@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "phoneNumber": "+1234567890",
  "password": "SecurePass123!",
  "roleIds": ["uuid1", "uuid2"]  // Array de IDs de roles
}
```

**Validaciones:**
- `email`: Requerido, formato válido, único en el tenant
- `firstName`: Requerido, 2-50 caracteres
- `lastName`: Requerido, 2-50 caracteres
- `password`: Requerido, mínimo 8 caracteres, al menos 1 mayúscula, 1 minúscula, 1 número
- `roleIds`: Opcional, si se omite se asigna rol "Customer"

**Respuesta 201:**
```json
{
  "id": "uuid",
  "email": "newuser@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "phoneNumber": "+1234567890",
  "isActive": true,
  "emailConfirmed": false,
  "roles": [
    {
      "id": "uuid",
      "name": "Customer",
      "isSystemRole": true
    }
  ],
  "createdAt": "2024-01-15T16:20:00Z"
}
```

**Respuesta 400:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "errors": {
    "email": ["Email already exists"],
    "password": ["Password must be at least 8 characters"]
  }
}
```

---

## 4. Actualizar Roles de Usuario

```http
PUT /admin/users/{id}/roles
```

**Permisos requeridos:** `customers:update`

**Path Parameters:**
- `id` (uuid): ID del usuario

**Request Body:**
```json
{
  "roleIds": ["uuid1", "uuid2", "uuid3"]
}
```

**Protección de lockout:**
- No puedes remover tu propio rol de administrador
- Debe tener al menos 1 rol asignado

**Respuesta 200:**
```json
{
  "id": "uuid",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "roles": [
    {
      "id": "uuid1",
      "name": "Admin",
      "isSystemRole": true
    },
    {
      "id": "uuid2",
      "name": "Sales Manager",
      "isSystemRole": false
    }
  ],
  "isActive": true,
  "createdAt": "2024-01-01T00:00:00Z"
}
```

**Respuesta 400 (Auto-lockout prevention):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Cannot remove your own admin role. This would lock you out."
}
```

---

## 5. Activar/Desactivar Usuario

```http
PATCH /admin/users/{id}/status
```

**Permisos requeridos:** `customers:update`

**Path Parameters:**
- `id` (uuid): ID del usuario

**Request Body:**
```json
{
  "isActive": true  // true = activar, false = desactivar
}
```

**Respuesta 200:**
```json
{
  "id": "uuid",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "roles": [...],
  "createdAt": "2024-01-01T00:00:00Z"
}
```

---

## 6. Eliminar Usuario (Soft Delete)

```http
DELETE /admin/users/{id}
```

**Permisos requeridos:** `customers:delete`

**Path Parameters:**
- `id` (uuid): ID del usuario

**Nota:** Es un soft delete, el usuario se marca como eliminado pero no se borra físicamente.

**Respuesta 204:** No Content (éxito)

**Respuesta 404:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "User not found"
}
```

---

# 🎭 Gestión de Roles

Base URL: `/admin/roles`

## 1. Listar Roles

```http
GET /admin/roles
```

**Permisos requeridos:** `users:view`

**Respuesta 200:**
```json
{
  "roles": [
    {
      "id": "uuid",
      "name": "SuperAdmin",
      "description": "Full system access",
      "isSystemRole": true,
      "usersCount": 5,
      "createdAt": "2024-01-01T00:00:00Z"
    },
    {
      "id": "uuid",
      "name": "Sales Manager",
      "description": "Manage sales and orders",
      "isSystemRole": false,
      "usersCount": 12,
      "createdAt": "2024-01-05T00:00:00Z"
    }
  ],
  "totalRoles": 5
}
```

---

## 2. Obtener Rol por ID

```http
GET /admin/roles/{roleId}
```

**Permisos requeridos:** `users:view`

**Path Parameters:**
- `roleId` (uuid): ID del rol

**Respuesta 200:**
```json
{
  "id": "uuid",
  "name": "Sales Manager",
  "description": "Manage sales and orders",
  "isSystemRole": false,
  "users": [
    {
      "id": "uuid",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe"
    }
  ],
  "permissions": [
    {
      "moduleCode": "sales",
      "moduleName": "Sales Management",
      "canView": true,
      "canCreate": true,
      "canUpdate": true,
      "canDelete": false
    }
  ],
  "usersCount": 12,
  "createdAt": "2024-01-05T00:00:00Z",
  "lastModifiedAt": "2024-01-10T12:00:00Z"
}
```

---

## 3. Crear Rol

```http
POST /admin/roles
```

**Permisos requeridos:** `users:create`

**Request Body:**
```json
{
  "name": "Inventory Manager",
  "description": "Manage products and inventory"
}
```

**Validaciones:**
- `name`: Requerido, 3-50 caracteres, único en el tenant
- `description`: Opcional, máximo 200 caracteres

**Respuesta 201:**
```json
{
  "id": "uuid",
  "name": "Inventory Manager",
  "description": "Manage products and inventory",
  "isSystemRole": false,
  "users": [],
  "permissions": [],
  "usersCount": 0,
  "createdAt": "2024-01-15T16:30:00Z"
}
```

**Respuesta 409 (Nombre duplicado):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "Role with this name already exists"
}
```

---

## 4. Actualizar Rol

```http
PUT /admin/roles/{roleId}
```

**Permisos requeridos:** `users:update`

**Path Parameters:**
- `roleId` (uuid): ID del rol

**Request Body:**
```json
{
  "name": "Inventory & Stock Manager",  // Opcional
  "description": "Updated description"  // Opcional
}
```

**Restricciones:**
- No se puede cambiar el nombre de roles del sistema (SuperAdmin, Customer)
- El nuevo nombre debe ser único

**Respuesta 200:**
```json
{
  "id": "uuid",
  "name": "Inventory & Stock Manager",
  "description": "Updated description",
  "isSystemRole": false,
  "users": [...],
  "permissions": [...],
  "usersCount": 8,
  "createdAt": "2024-01-05T00:00:00Z",
  "lastModifiedAt": "2024-01-15T16:35:00Z"
}
```

---

## 5. Eliminar Rol

```http
DELETE /admin/roles/{roleId}
```

**Permisos requeridos:** `users:delete`

**Path Parameters:**
- `roleId` (uuid): ID del rol

**Restricciones:**
- No se pueden eliminar roles del sistema (SuperAdmin, Customer)
- No se puede eliminar un rol que tiene usuarios asignados

**Respuesta 204:** No Content (éxito)

**Respuesta 400:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Cannot delete system role"
}
```

**Respuesta 409:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "Cannot delete role with assigned users. Remove all users first."
}
```

---

# 🔑 Gestión de Permisos

## 1. Obtener Módulos Disponibles

```http
GET /admin/roles/available-modules
```

**Permisos requeridos:** `users:view`

**Descripción:** Retorna el catálogo completo de módulos y permisos disponibles para asignar a roles.

**Respuesta 200:**
```json
{
  "modules": [
    {
      "code": "inventory",
      "name": "Inventory Management",
      "description": "Manage products, categories, and stock",
      "icon": "📦",
      "isActive": true,
      "availablePermissions": ["view", "create", "update", "delete"]
    },
    {
      "code": "sales",
      "name": "Sales Management",
      "description": "Manage orders and sales",
      "icon": "💰",
      "isActive": true,
      "availablePermissions": ["view", "create", "update", "delete"]
    },
    {
      "code": "customers",
      "name": "Customer Management",
      "description": "Manage customers and users",
      "icon": "👥",
      "isActive": true,
      "availablePermissions": ["view", "create", "update", "delete"]
    },
    {
      "code": "users",
      "name": "User & Role Management",
      "description": "Manage system users and permissions",
      "icon": "🔐",
      "isActive": true,
      "availablePermissions": ["view", "create", "update", "delete"]
    },
    {
      "code": "settings",
      "name": "Store Settings",
      "description": "Configure store branding and settings",
      "icon": "⚙️",
      "isActive": true,
      "availablePermissions": ["view", "update"]
    },
    {
      "code": "marketing",
      "name": "Marketing & Promotions",
      "description": "Manage campaigns, banners, and coupons",
      "icon": "📢",
      "isActive": true,
      "availablePermissions": ["view", "create", "update", "delete"]
    },
    {
      "code": "reports",
      "name": "Reports & Analytics",
      "description": "View business reports and analytics",
      "icon": "📊",
      "isActive": true,
      "availablePermissions": ["view"]
    }
  ]
}
```

---

## 2. Obtener Permisos de un Rol

```http
GET /admin/roles/{roleId}/permissions
```

**Permisos requeridos:** `users:view`

**Path Parameters:**
- `roleId` (uuid): ID del rol

**Respuesta 200:**
```json
{
  "roleId": "uuid",
  "roleName": "Sales Manager",
  "permissions": [
    {
      "moduleCode": "sales",
      "moduleName": "Sales Management",
      "canView": true,
      "canCreate": true,
      "canUpdate": true,
      "canDelete": false
    },
    {
      "moduleCode": "customers",
      "moduleName": "Customer Management",
      "canView": true,
      "canCreate": false,
      "canUpdate": false,
      "canDelete": false
    },
    {
      "moduleCode": "reports",
      "moduleName": "Reports & Analytics",
      "canView": true,
      "canCreate": false,
      "canUpdate": false,
      "canDelete": false
    }
  ]
}
```

---

## 3. Actualizar Permisos de un Rol

```http
PUT /admin/roles/{roleId}/permissions
```

**Permisos requeridos:** `users:update`

**Path Parameters:**
- `roleId` (uuid): ID del rol

**Request Body:**
```json
{
  "permissions": [
    {
      "moduleCode": "inventory",
      "canView": true,
      "canCreate": true,
      "canUpdate": true,
      "canDelete": false
    },
    {
      "moduleCode": "sales",
      "canView": true,
      "canCreate": true,
      "canUpdate": true,
      "canDelete": true
    }
  ]
}
```

**Notas:**
- Solo se actualizan los módulos enviados
- Los módulos no enviados mantienen sus permisos actuales
- Los módulos enviados con todos los permisos en `false` se eliminan

**Respuesta 200:**
```json
{
  "roleId": "uuid",
  "roleName": "Sales Manager",
  "permissions": [
    {
      "moduleCode": "inventory",
      "moduleName": "Inventory Management",
      "canView": true,
      "canCreate": true,
      "canUpdate": true,
      "canDelete": false
    },
    {
      "moduleCode": "sales",
      "moduleName": "Sales Management",
      "canView": true,
      "canCreate": true,
      "canUpdate": true,
      "canDelete": true
    }
  ]
}
```

**Respuesta 400:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Error",
  "status": 400,
  "errors": {
    "permissions[0].moduleCode": ["Module 'invalid-module' does not exist"]
  }
}
```

---

# 📦 DTOs y Modelos

## User Models

```typescript
interface TenantUserDetailDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  isActive: boolean;
  emailConfirmed: boolean;
  roles: RoleSummaryDto[];
  createdAt: string;
  lastLoginAt?: string;
  lastModifiedAt?: string;
}

interface AdminUsersResponse {
  users: TenantUserDetailDto[];
  totalUsers: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
}

interface CreateUserRequest {
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  password: string;
  roleIds?: string[];
}

interface UpdateUserRolesRequest {
  roleIds: string[];
}

interface UpdateUserActiveStatusRequest {
  isActive: boolean;
}
```

## Role Models

```typescript
interface RoleDetailDto {
  id: string;
  name: string;
  description: string;
  isSystemRole: boolean;
  users: UserSummaryDto[];
  permissions: ModulePermissionDto[];
  usersCount: number;
  createdAt: string;
  lastModifiedAt?: string;
}

interface RoleSummaryDto {
  id: string;
  name: string;
  description?: string;
  isSystemRole: boolean;
}

interface RolesResponse {
  roles: RoleSummaryDto[];
  totalRoles: number;
}

interface CreateRoleRequest {
  name: string;
  description?: string;
}

interface UpdateRoleRequest {
  name?: string;
  description?: string;
}
```

## Permission Models

```typescript
interface ModuleDto {
  code: string;
  name: string;
  description: string;
  icon: string;
  isActive: boolean;
  availablePermissions: string[];
}

interface AvailableModulesResponse {
  modules: ModuleDto[];
}

interface ModulePermissionDto {
  moduleCode: string;
  moduleName: string;
  canView: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

interface RolePermissionsResponse {
  roleId: string;
  roleName: string;
  permissions: ModulePermissionDto[];
}

interface UpdateRolePermissionsRequest {
  permissions: {
    moduleCode: string;
    canView: boolean;
    canCreate: boolean;
    canUpdate: boolean;
    canDelete: boolean;
  }[];
}

interface ModulePermissions {
  moduleCode: string;
  canView: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}
```

---

# 🔒 Códigos de Módulos

Lista completa de módulos disponibles:

| Código | Nombre | Descripción | Permisos |
|--------|--------|-------------|----------|
| `inventory` | Inventory Management | Gestión de productos y categorías | view, create, update, delete |
| `sales` | Sales Management | Gestión de órdenes y ventas | view, create, update, delete |
| `customers` | Customer Management | Gestión de clientes | view, create, update, delete |
| `users` | User & Role Management | Gestión de usuarios y roles | view, create, update, delete |
| `settings` | Store Settings | Configuración de la tienda | view, update |
| `marketing` | Marketing & Promotions | Campañas y promociones | view, create, update, delete |
| `reports` | Reports & Analytics | Reportes y análisis | view |

---

# 🚨 Códigos de Error Comunes

| Status | Tipo | Descripción |
|--------|------|-------------|
| 400 | Bad Request | Datos de entrada inválidos |
| 401 | Unauthorized | Token JWT inválido o expirado |
| 403 | Forbidden | Sin permisos para este módulo/acción |
| 404 | Not Found | Recurso no encontrado |
| 409 | Conflict | Conflicto (ej: nombre duplicado) |
| 500 | Internal Server Error | Error del servidor |

---

# 📝 Ejemplos de Uso

## Ejemplo: Crear un rol y asignar permisos

```typescript
// 1. Crear rol
const createRoleResponse = await fetch('/admin/roles', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'X-Tenant-Slug': 'my-store',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    name: 'Warehouse Manager',
    description: 'Manages inventory and stock'
  })
});

const role = await createRoleResponse.json();

// 2. Asignar permisos al rol
await fetch(`/admin/roles/${role.id}/permissions`, {
  method: 'PUT',
  headers: {
    'Authorization': `Bearer ${token}`,
    'X-Tenant-Slug': 'my-store',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    permissions: [
      {
        moduleCode: 'inventory',
        canView: true,
        canCreate: true,
        canUpdate: true,
        canDelete: true
      }
    ]
  })
});
```

## Ejemplo: Crear usuario y asignar rol

```typescript
// 1. Crear usuario
const createUserResponse = await fetch('/admin/users', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'X-Tenant-Slug': 'my-store',
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    email: 'john@example.com',
    firstName: 'John',
    lastName: 'Doe',
    password: 'SecurePass123!',
    roleIds: [role.id]
  })
});

const user = await createUserResponse.json();
```

---

# ✨ Características Especiales

## Protección contra Auto-Lockout

Al actualizar roles de usuarios, el sistema previene que un administrador se quite su propio rol administrativo, evitando quedar bloqueado del sistema.

## Roles del Sistema

Los roles `SuperAdmin` y `Customer` son roles del sistema y tienen restricciones especiales:
- No se puede cambiar su nombre
- No se pueden eliminar
- No se pueden modificar sus permisos base

## Soft Delete

La eliminación de usuarios es "soft delete": el usuario se marca como eliminado pero no se borra de la base de datos, permitiendo auditoría y recuperación.

## Permisos Acumulativos

Si un usuario tiene múltiples roles, los permisos se combinan usando lógica OR (el más permisivo gana).

---

**Fecha de documentación:** 12 de febrero de 2026  
**Versión API:** 1.0
