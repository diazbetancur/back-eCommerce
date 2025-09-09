# eCommerce Backend API

API backend para sistema de eCommerce desarrollado en .NET 8 con arquitectura por capas.

## ?? Caracter�sticas

- **Arquitectura por capas**: Domain, Application, Infrastructure, API
- **Entity Framework Core**: ORM con PostgreSQL
- **Google Cloud Storage**: Para manejo de archivos e im�genes
- **AutoMapper**: Mapeo entre entidades y DTOs
- **Serilog**: Sistema de logging avanzado
- **JWT**: Autenticaci�n y autorizaci�n
- **Swagger**: Documentaci�n de API

## ?? Prerrequisitos

- .NET 8 SDK
- PostgreSQL
- Google Cloud Storage (Firebase Storage)
- Visual Studio 2022 o VS Code

## ?? Configuraci�n

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
      // Tu configuraci�n de Firebase/Google Cloud
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
??? Api-eCommerce/          # Capa de presentaci�n (API)
?   ??? Controllers/        # Controladores de API
?   ??? Handlers/          # Middlewares y configuraciones
??? CC.Application/         # Capa de aplicaci�n
?   ??? Services/          # Servicios de aplicaci�n
??? CC.Domain/             # Capa de dominio
?   ??? Entities/          # Entidades del dominio
?   ??? Interfaces/        # Interfaces
?   ??? Dto/              # Data Transfer Objects
??? CC.Infrastructure/     # Capa de infraestructura
    ??? Configurations/    # Configuraci�n de BD
    ??? Repositories/      # Repositorios
    ??? Services/         # Servicios de infraestructura
```

## ?? Endpoints Principales

### Productos
- `GET /api/Product` - Lista todos los productos
- `GET /api/Product/{id}` - Obtiene producto por ID
- `GET /api/Product/category/{categoryId}` - Productos por categor�a
- `GET /api/Product/search/{searchTerm}` - B�squeda de productos
- `POST /api/Product` - Crear producto
- `PUT /api/Product` - Actualizar producto
- `DELETE /api/Product/{id}` - Eliminar producto

### Categor�as
- `GET /api/Category` - Lista todas las categor�as activas
- `POST /api/Category` - Crear categor�a (con imagen)
- `PUT /api/Category` - Actualizar categor�a
- `DELETE /api/Category/{id}` - Eliminar categor�a

## ??? Tecnolog�as

- **.NET 8**: Framework principal
- **Entity Framework Core**: ORM
- **PostgreSQL**: Base de datos
- **Google Cloud Storage**: Almacenamiento de archivos
- **AutoMapper**: Mapeo de objetos
- **Serilog**: Logging
- **Swagger/OpenAPI**: Documentaci�n
- **JWT Bearer**: Autenticaci�n

## ?? Licencia

Este proyecto est� bajo la Licencia MIT.

## ????? Author

**Alex D�az Betancur**
- GitHub: [@diazbetancur](https://github.com/diazbetancur)