# 📋 CRUD de Categorías - Documentación Completa

## ✅ Implementación Completada

Se ha implementado un **CRUD completo de Categorías** para el e-Commerce multi-tenant con las siguientes características:

---

## 🎯 Características Implementadas

### 1. **Módulo "categories" en el Sistema de Permisos**
- ✅ Agregado al seeder de módulos (`TenantModulesSeeder.cs`)
- ✅ Permisos configurados por rol:
  - **Admin**: Ver, Crear, Actualizar, Eliminar (acceso completo)
  - **Manager**: Ver, Crear, Actualizar (sin eliminar)
  - **Viewer**: Solo Ver

### 2. **Endpoints RESTful con Minimal API**
Todos los endpoints están en `Api-eCommerce/Endpoints/CategoryEndpoints.cs`

#### Endpoints Públicos (sin autenticación)
- `GET /api/categories` - Listar con paginación, búsqueda y filtros
- `GET /api/categories/{id}` - Obtener por ID
- `GET /api/categories/slug/{slug}` - Obtener por slug (URL amigable)

#### Endpoints Protegidos (requieren autenticación + permisos)
- `POST /api/categories` - Crear (requiere permiso "create")
- `PUT /api/categories/{id}` - Actualizar (requiere permiso "update")
- `DELETE /api/categories/{id}` - Eliminar (requiere permiso "delete")

### 3. **Servicio de Gestión**
Archivo: `CC.Aplication/Catalog/CategoryManagementService.cs`

Funcionalidades:
- ✅ Generación automática de slug SEO-friendly
- ✅ Validación de nombre único
- ✅ Gestión de slugs duplicados (agrega sufijo numérico)
- ✅ Preparado para jerarquías (campo `parentId`)
- ✅ Eliminación física con desvinculación automática de productos

### 4. **DTOs Completos**
Archivo: `CC.Aplication/Catalog/CategoryDtos.cs`

- `CreateCategoryRequest` - Para crear
- `UpdateCategoryRequest` - Para actualizar
- `CategoryResponse` - Respuesta detallada
- `CategoryListItem` - Item simplificado para listados
- `CategoryListResponse` - Respuesta paginada

---

## 📊 Estructura de Datos

### Request para Crear
```json
{
  "name": "Electrónica",
  "description": "Productos electrónicos y gadgets",
  "imageUrl": "https://storage.com/electronics.jpg",
  "isActive": true,
  "parentId": null
}
```

### Response
```json
{
  "id": "a1b2c3d4-...",
  "name": "Electrónica",
  "slug": "electronica",
  "description": "Productos electrónicos y gadgets",
  "imageUrl": "https://storage.com/electronics.jpg",
  "isActive": true,
  "productCount": 45,
  "parentId": null,
  "createdAt": "2025-12-21T15:00:00Z",
  "updatedAt": null
}
```

### Response de Listado Paginado
```json
{
  "items": [
    {
      "id": "a1b2c3d4-...",
      "name": "Electrónica",
      "slug": "electronica",
      "imageUrl": "https://storage.com/electronics.jpg",
      "isActive": true,
      "productCount": 45
    }
  ],
  "total": 8,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

---

## 🔐 Sistema de Permisos

### Permisos por Rol

| Rol | Ver | Crear | Actualizar | Eliminar |
|-----|-----|-------|------------|----------|
| **Admin** | ✅ | ✅ | ✅ | ✅ |
| **Manager** | ✅ | ✅ | ✅ | ❌ |
| **Viewer** | ✅ | ❌ | ❌ | ❌ |

### Autorización en Endpoints
- Los endpoints públicos usan `AllowAnonymous()`
- Los endpoints protegidos usan:
  - `RequireAuthorization()` - Valida que esté autenticado
  - `ModuleAuthorizationFilter` - Valida permisos del módulo
  - `RequireModuleAttribute("categories", "create|update|delete")` - Especifica el permiso requerido

---

## 🎨 Validaciones

### Nombre
- **Requerido**
- Longitud: entre 3 y 100 caracteres
- Único por tenant (case-insensitive)

### Descripción
- Opcional
- Máximo 500 caracteres

### ImageUrl
- Opcional
- Debe ser una URL válida si se proporciona

### Slug
- Generado automáticamente del nombre
- URL-friendly: `"Ropa de Mujer"` → `"ropa-de-mujer"`
- Si existe duplicado, agrega sufijo: `"electronica-1"`, `"electronica-2"`
- Caracteres removidos: acentos, espacios → guiones, caracteres especiales

---

## 🗑️ Eliminación de Categorías

Cuando se elimina una categoría:

1. **Se desvinculan automáticamente los productos**
   - Se eliminan los registros de la tabla `ProductCategories`
   - Los productos NO se eliminan, solo pierden la relación

2. **Se desvinculan las subcategorías** (preparado para futuro)
   - Si tiene categorías hijas, se les quita el `parentId`
   - Las subcategorías pasan a ser de nivel raíz

3. **Eliminación física** (hard delete)
   - La categoría se elimina permanentemente de la base de datos

---

## 📁 Archivos Creados/Modificados

### Nuevos Archivos
1. `CC.Aplication/Catalog/CategoryDtos.cs` - DTOs
2. `CC.Aplication/Catalog/CategoryManagementService.cs` - Lógica de negocio
3. `Api-eCommerce/Endpoints/CategoryEndpoints.cs` - API endpoints
4. `dev/categories-api-examples.http` - Ejemplos completos de uso

### Archivos Modificados
1. `CC.Infraestructure/TenantSeeders/TenantModulesSeeder.cs` - Agregado módulo "categories"
2. `Api-eCommerce/Program.cs` - Registrado servicio y endpoints

---

## 🚀 Cómo Probar

### 1. Reinicia el servidor
```bash
cd Api-eCommerce
dotnet run
```

### 2. El seeder agregará automáticamente el módulo "categories"
Al iniciar, verás en los logs:
```
➕ New module detected: categories - Categorías
🔐 Auto-granting Admin full access to new module: categories
```

### 3. Haz login como Admin
```bash
POST http://localhost:5093/auth/login
X-Tenant-Slug: test
Content-Type: application/json

{
  "email": "admin@admin.com",
  "password": "qTU=02Ee"
}
```

### 4. Crea una categoría
```bash
POST http://localhost:5093/api/categories
X-Tenant-Slug: test
Authorization: Bearer {TOKEN_DEL_PASO_3}
Content-Type: application/json

{
  "name": "Electrónica",
  "description": "Productos electrónicos",
  "isActive": true
}
```

### 5. Lista las categorías (público)
```bash
GET http://localhost:5093/api/categories
X-Tenant-Slug: test
```

---

## 📝 Ejemplos de Uso Completos

Todos los ejemplos están en: `dev/categories-api-examples.http`

Incluye:
- ✅ Listado con paginación
- ✅ Búsqueda y filtros
- ✅ Obtener por ID y por slug
- ✅ Crear categorías
- ✅ Actualizar (cambio de nombre actualiza slug automáticamente)
- ✅ Eliminar
- ✅ Manejo de errores
- ✅ Flujo completo E2E

---

## 🔄 Preparado para el Futuro

### Jerarquías de Categorías
Aunque actualmente las categorías son planas, el sistema está **completamente preparado** para soportar jerarquías:

- ✅ Campo `parentId` en la entidad
- ✅ Validaciones en el servicio
- ✅ Lógica de desvinculación en eliminación
- ✅ DTOs incluyen `parentId`

**Para activar jerarquías**, solo necesitas:
1. Agregar endpoints para listar subcategorías
2. Implementar consultas recursivas si lo deseas
3. Validar niveles máximos de profundidad

### Características Adicionales Sugeridas
- 📸 **Upload de imágenes**: Integrar con el servicio de storage existente
- 🔢 **Reordenamiento**: Implementar drag & drop usando el campo `DisplayOrder`
- 🌳 **Vista de árbol**: Para visualizar jerarquías cuando se activen
- 📊 **Estadísticas**: Productos por categoría, categorías más vendidas
- 🔍 **Búsqueda avanzada**: Filtros combinados con autocompletado

---

## ✨ Ventajas de la Implementación

1. **Escalable**: Preparado para jerarquías sin cambios en la estructura
2. **Seguro**: Sistema de permisos granular por rol
3. **SEO-Friendly**: Slugs automáticos para URLs limpias
4. **Performante**: Paginación y consultas optimizadas
5. **Mantenible**: Sigue los estándares de la arquitectura existente
6. **Documentado**: Ejemplos completos y documentación detallada

---

## 🎉 Resumen

Has obtenido un **CRUD completo y production-ready** de Categorías con:

✅ Sistema de permisos integrado
✅ Validaciones robustas
✅ Generación automática de slugs
✅ Eliminación segura con desvinculación
✅ API pública para el frontend
✅ API protegida para administración
✅ Ejemplos de uso completos
✅ Preparado para jerarquías
✅ Arquitectura limpia y escalable

**¡Todo listo para usar!** 🚀
