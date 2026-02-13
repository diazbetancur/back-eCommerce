# RBAC API — Contratos HTTP (Users, Roles, Permissions)

## Tabla de Contenidos
1. [Resumen](#1-resumen)
2. [Autenticación y Autorización](#2-autenticación-y-autorización)
3. [Convenciones del API](#3-convenciones-del-api)
4. [Endpoints - Usuarios (Users)](#4-endpoints---usuarios-users)
5. [Endpoints - Roles (Roles)](#5-endpoints---roles-roles)
6. [Endpoints - Permisos (Permissions)](#6-endpoints---permisos-permissions)
7. [Endpoints - Asignación de Roles a Usuario](#7-endpoints---asignación-de-roles-a-usuario)
8. [Modelos / DTOs (Schemas)](#8-modelos--dtos-schemas)
9. [Flujos recomendados para frontend](#9-flujos-recomendados-para-frontend)
10. [Checklist de implementación (Frontend)](#10-checklist-de-implementación-frontend)

---

## 1) Resumen

Este módulo documenta los contratos HTTP del API REST para administración RBAC (usuarios, roles, permisos). Está orientado a frontend devs (Angular/React) y cubre:
- CRUD de usuarios
- Asignación de roles
- Consulta de permisos efectivos

**Requisitos:**
- Autenticación JWT (`Authorization: Bearer <token>`)
- Header `X-Tenant-Slug` para rutas `/admin/*`
- Base URL: variable (ej: `https://api.example.com`)

**Convenciones:**
- IDs: GUID/UUID string
- Fechas: ISO 8601
- Paginación: si aplica, documentada por endpoint
- Formato de error: ProblemDetails (RFC 7807) o `{ "error": "..." }`

---

## 2) Autenticación y Autorización

- Header obligatorio: `Authorization: Bearer <token>`
- Header tenant: `X-Tenant-Slug: <slug>` (rutas `/admin/*`)
- Permisos requeridos: cada endpoint indica el módulo y permiso necesario (ej: `RequireModule("customers", "view")`)

**Errores comunes:**
- 401 Unauthorized: token inválido o ausente
- 403 Forbidden: token válido pero sin permiso
- 409 Conflict: tenant no resuelto

Ejemplo 401:
```json
{
  "type": "about:blank",
  "title": "Invalid Token",
  "status": 401,
  "detail": "User ID not found in token"
}
```

---

## 3) Convenciones del API

### 3.1 Base URL
- Ejemplo: `https://api.example.com`
- Rutas admin: `/admin/users`, `/admin/users/{id}/role`, etc.
- Rutas user: `/me/modules`, `/me/modules/{moduleCode}/permissions`

### 3.2 Headers
- `Content-Type: application/json`
- `Authorization: Bearer <token>`
- `X-Tenant-Slug: <slug>` (rutas `/admin/*`)

### 3.3 Formato de errores
- ProblemDetails (RFC 7807) o `{ "error": "..." }`

Ejemplo 400:
```json
{
  "type": "about:blank",
  "title": "Operation Failed",
  "status": 400,
  "detail": "Invalid request data"
}
```
Ejemplo 404:
```json
{ "error": "User not found" }
```
Ejemplo 409:
```json
{ "error": "Email already exists" }
```
Ejemplo 402 (límite de plan):
```json
{
  "title": "Plan Limit Exceeded",
  "status": 402,
  "detail": "Has alcanzado el límite de usuarios de tu plan.",
  "extensions": { "limitCode": "MaxUsers", "limitValue": 10, "currentValue": 12 }
}
```

### 3.4 Tipos comunes
- UUID/GUID: `"id": "0f8fad5b-d9cb-469f-a165-70867728950e"`
- Fecha: `"2025-01-23T14:17:00Z"`
- Paginación: (no implementada en `/admin/users` actual)
- Sorting: (no implementado)

---

## 4) Endpoints - Usuarios (Users)

### 4.1 Listar usuarios
- `GET /admin/users`
- Headers: `Authorization`, `X-Tenant-Slug`
- Permiso: `RequireModule("customers", "view")`
- Response 200:
```json
{
  "users": [
    {
      "id": "ea7c5285-5c93-4008-82ce-99950607b914",
      "email": "admin@yourdomain.com",
      "roles": ["SuperAdmin"],
      "isActive": true,
      "createdAt": "2025-01-10T12:00:00Z"
    }
  ]
}
```
- Errores: 401, 403, 409 (tenant)

### 4.2 Consultar usuario por id
- `GET /admin/users/{id}` — **TBD: no implementado**

### 4.3 Crear usuario
- `POST /admin/users`
- Body:
```json
{
  "email": "new.user@tenant.com",
  "password": "StrongP@ss1"
}
```
- Response 201:
```json
{
  "id": "9f8e7d6c-5b4a-3c2d-1e0f-9a8b7c6d5e4f",
  "email": "new.user@tenant.com",
  "roles": [],
  "isActive": true,
  "createdAt": "2025-01-23T15:00:00Z"
}
```
- Errores: 409 (email), 402 (plan), 401/403

### 4.4 Asignar rol a usuario
- `PATCH /admin/users/{id}/role`
- Body:
```json
{ "roleName": "Customer" }
```
- Response 200:
```json
{
  "id": "ea7c5285-5c93-4008-82ce-99950607b914",
  "email": "staff@tenant.com",
  "roles": ["Customer", "Staff"],
  "isActive": true,
  "createdAt": "2025-01-11T09:30:00Z"
}
```
- Errores: 404 (user/role), 401/403

### 4.5 Eliminar usuario
- `DELETE /admin/users/{id}` — **TBD: no implementado**

---

## 5) Endpoints - Roles (Roles)

- CRUD de roles (`GET/POST/PUT/DELETE /admin/roles`) — **TBD: no implementado**

---

## 6) Endpoints - Permisos (Permissions)

### 6.1 Listar módulos y permisos del usuario
- `GET /me/modules`
- Headers: `Authorization`
- Response 200:
```json
{
  "modules": [
    {
      "code": "inventory",
      "name": "Inventario",
      "description": "Gestión de tiendas y stock multi-ubicación",
      "iconName": "warehouse",
      "permissions": {
        "canView": true,
        "canCreate": true,
        "canUpdate": true,
        "canDelete": true
      }
    }
  ]
}
```

### 6.2 Consultar permisos para un módulo
- `GET /me/modules/{moduleCode}/permissions`
- Response 200:
```json
{
  "moduleCode": "inventory",
  "canView": true,
  "canCreate": true,
  "canUpdate": true,
  "canDelete": true
}
```

### 6.3/6.4 Asignar/quitar permisos a rol
- **TBD: no implementado**

---

## 7) Endpoints - Asignación de Roles a Usuario

### 7.1 Asignar rol
- `PATCH /admin/users/{userId}/role` (ver 4.4)

### 7.2 Quitar rol
- **TBD: no implementado**

### 7.3 Reemplazar roles
- **TBD: no implementado**

---

## 8) Modelos / DTOs (Schemas)

### TenantUserListItemDto
| Campo      | Tipo         | Requerido | Descripción           |
|------------|--------------|-----------|-----------------------|
| id         | string (GUID)| sí        | Identificador usuario |
| email      | string       | sí        | Email                 |
| roles      | string[]     | sí        | Nombres de roles      |
| isActive   | boolean      | sí        | Activo                |
| createdAt  | string (ISO) | sí        | Fecha creación        |

Ejemplo:
```json
{
  "id": "ea7c5285-5c93-4008-82ce-99950607b914",
  "email": "admin@yourdomain.com",
  "roles": ["SuperAdmin"],
  "isActive": true,
  "createdAt": "2025-01-10T12:00:00Z"
}
```

### CreateTenantUserRequest
| Campo   | Tipo   | Requerido | Descripción         |
|---------|--------|-----------|---------------------|
| email   | string | sí        | Email único         |
| password| string | sí        | Contraseña          |

### AssignRoleRequest
| Campo    | Tipo   | Requerido | Descripción         |
|----------|--------|-----------|---------------------|
| roleName | string | sí        | Nombre del rol      |

### ModulesResponse / ModuleResponse / PermissionsResponse
| Campo         | Tipo     | Requerido | Descripción                |
|---------------|----------|-----------|----------------------------|
| code          | string   | sí        | Código del módulo          |
| name          | string   | sí        | Nombre del módulo          |
| description   | string   | no        | Descripción                |
| iconName      | string   | no        | Icono                      |
| permissions   | object   | sí        | Permisos (ver abajo)       |

#### PermissionsResponse
| Campo      | Tipo    | Requerido | Descripción |
|------------|---------|-----------|-------------|
| canView    | boolean | sí        | Puede ver   |
| canCreate  | boolean | sí        | Puede crear |
| canUpdate  | boolean | sí        | Puede editar|
| canDelete  | boolean | sí        | Puede borrar|

### ModulePermissions
| Campo      | Tipo    | Requerido | Descripción |
|------------|---------|-----------|-------------|
| moduleCode | string  | sí        | Código      |
| canView    | boolean | sí        | Puede ver   |
| canCreate  | boolean | sí        | Puede crear |
| canUpdate  | boolean | sí        | Puede editar|
| canDelete  | boolean | sí        | Puede borrar|

### AdminUsersResponse
| Campo | Tipo                         | Requerido | Descripción         |
|-------|------------------------------|-----------|---------------------|
| users | TenantUserListItemDto[]      | sí        | Lista de usuarios   |

### Error / ProblemDetails
- Ver ejemplos en sección 3.3

---

## 9) Flujos recomendados para frontend

**Pantalla Usuarios:**
1. `GET /admin/users` → mostrar lista
2. Crear: `POST /admin/users` → manejar 409/402
3. Asignar rol: `PATCH /admin/users/{id}/role`
4. (TBD) Eliminar usuario: endpoint no implementado

**Pantalla Roles:**
- (TBD) No hay endpoints CRUD roles

**Permisos efectivos:**
- `GET /me/modules` para menú y acciones
- `GET /me/modules/{moduleCode}/permissions` para detalle

---

## 10) Checklist de implementación (Frontend)

- Cliente HTTP base (axios/fetch/Angular HttpClient)
- Interceptors: auth token, manejo 401/403, `X-Tenant-Slug`
- Tipos TypeScript recomendados:
```ts
interface TenantUserListItemDto {
  id: string;
  email: string;
  roles: string[];
  isActive: boolean;
  createdAt: string;
}
interface CreateTenantUserRequest { email: string; password: string; }
interface AssignRoleRequest { roleName: string; }
interface PermissionsResponse { canView: boolean; canCreate: boolean; canUpdate: boolean; canDelete: boolean; }
interface ModuleResponse { code: string; name: string; description?: string; iconName?: string; permissions: PermissionsResponse; }
```
- Estados UI: loading, empty, error, retry

---

**Notas finales / TODOs**
- CRUD de roles y permisos por rol: **TBD** (no implementado)
- GET/DELETE usuario por id: **TBD**
- Paginación en usuarios: **TBD**
- Validaciones input: solo email duplicado, no hay validación de formato/strength
