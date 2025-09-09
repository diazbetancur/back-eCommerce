# eCommerce Backend API

API backend para sistema de eCommerce desarrollado en .NET 8 con arquitectura por capas.

## ?? Características

- **Arquitectura por capas**: Domain, Application, Infrastructure, API
- **Entity Framework Core**: ORM con PostgreSQL
- **Google Cloud Storage**: Para manejo de archivos e imágenes
- **AutoMapper**: Mapeo entre entidades y DTOs
- **Serilog**: Sistema de logging avanzado
- **JWT**: Autenticación y autorización
- **Swagger**: Documentación de API

## ?? Prerrequisitos

- .NET 8 SDK
- PostgreSQL
- Google Cloud Storage (Firebase Storage)
- Visual Studio 2022 o VS Code

## ?? Configuración

1. **Clona el repositorio**
```bash
git clone https://github.com/diazbetancur/back-eCommerce.git
cd back-eCommerce
```

2. **Configura appsettings.json**
```json
{
  "ConnectionStrings": {
    "PgSQL": "tu-cadena-de-conexion-postgresql"
  },
  "GoogleStorage": {
    "BucketName": "tu-bucket-name",
    "firebase": {
      // Tu configuración de Firebase/Google Cloud
    }
  },
  "jwtKey": "tu-clave-jwt-secreta"
}
```

3. **Ejecuta las migraciones**
```bash
dotnet ef database update --project CC.Infraestructure --startup-project Api-eCommerce
```

4. **Ejecuta el proyecto**
```bash
dotnet run --project Api-eCommerce
```

## ??? Estructura del Proyecto

```
??? Api-eCommerce/          # Capa de presentación (API)
?   ??? Controllers/        # Controladores de API
?   ??? Handlers/          # Middlewares y configuraciones
??? CC.Application/         # Capa de aplicación
?   ??? Services/          # Servicios de aplicación
??? CC.Domain/             # Capa de dominio
?   ??? Entities/          # Entidades del dominio
?   ??? Interfaces/        # Interfaces
?   ??? Dto/              # Data Transfer Objects
??? CC.Infrastructure/     # Capa de infraestructura
    ??? Configurations/    # Configuración de BD
    ??? Repositories/      # Repositorios
    ??? Services/         # Servicios de infraestructura
```

## ?? Endpoints Principales

### Productos
- `GET /api/Product` - Lista todos los productos
- `GET /api/Product/{id}` - Obtiene producto por ID
- `GET /api/Product/category/{categoryId}` - Productos por categoría
- `GET /api/Product/search/{searchTerm}` - Búsqueda de productos
- `POST /api/Product` - Crear producto
- `PUT /api/Product` - Actualizar producto
- `DELETE /api/Product/{id}` - Eliminar producto

### Categorías
- `GET /api/Category` - Lista todas las categorías activas
- `POST /api/Category` - Crear categoría (con imagen)
- `PUT /api/Category` - Actualizar categoría
- `DELETE /api/Category/{id}` - Eliminar categoría

## ??? Tecnologías

- **.NET 8**: Framework principal
- **Entity Framework Core**: ORM
- **PostgreSQL**: Base de datos
- **Google Cloud Storage**: Almacenamiento de archivos
- **AutoMapper**: Mapeo de objetos
- **Serilog**: Logging
- **Swagger/OpenAPI**: Documentación
- **JWT Bearer**: Autenticación

## ?? Licencia

Este proyecto está bajo la Licencia MIT.

## ????? Author

**Alex Díaz Betancur**
- GitHub: [@diazbetancur](https://github.com/diazbetancur)