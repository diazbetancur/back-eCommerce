using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api_eCommerce.Extensions
{
    /// <summary>
    /// Operation filter que agrega el header X-Tenant-Slug a la documentación de Swagger
    /// para endpoints que requieren resolución de tenant
    /// </summary>
    public class SwaggerTenantOperationFilter : IOperationFilter
    {
        // Rutas que NO requieren el header de tenant
        private static readonly string[] ExcludedPaths = 
        {
            "/swagger",
            "/health",
            "/provision/tenants/init",
            "/provision/tenants/confirm",
            "/superadmin"
        };

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Obtener la ruta del endpoint
            var path = context.ApiDescription.RelativePath?.ToLower() ?? string.Empty;

            // Verificar si la ruta está excluida
            if (IsExcludedPath(path))
            {
                return;
            }

            // Inicializar parámetros si es null
            operation.Parameters ??= new List<OpenApiParameter>();

            // Agregar el header X-Tenant-Slug
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Tenant-Slug",
                In = ParameterLocation.Header,
                Required = true,
                Description = @"
**Slug del tenant** (identificador único del inquilino)

Este header identifica a qué tenant pertenece la solicitud. Cada tenant tiene su propia base de datos y configuración aislada.

### ?? Formato:
- Solo letras minúsculas (a-z)
- Números (0-9)
- Guiones (-)
- Longitud mínima: 3 caracteres
- Longitud máxima: 50 caracteres

### ?? Ejemplos válidos:
- `acme`
- `demo-store`
- `mi-tienda-123`
- `company-xyz`

### ? Ejemplos inválidos:
- `ACME` (no mayúsculas)
- `my_store` (no guiones bajos)
- `ab` (muy corto)
- `my store` (no espacios)

### ?? Cómo obtener el slug:
1. **Registro**: El slug se define al crear el tenant en `/provision/tenants/init`
2. **Configuración pública**: Puedes consultar `/public/tenant-config` con el slug
3. **Administración**: SuperAdmin puede listar todos los tenants

### ?? Notas importantes:
- El slug es **case-sensitive** en algunos sistemas, pero se recomienda usar siempre minúsculas
- El tenant debe estar en estado `Ready` para que las solicitudes funcionen
- Si el tenant no existe, recibirás un error 404
- Si el tenant no está activo, recibirás un error 403
",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Pattern = "^[a-z0-9-]{3,50}$",
                    MinLength = 3,
                    MaxLength = 50,
                    Example = new Microsoft.OpenApi.Any.OpenApiString("acme")
                },
                Examples = new Dictionary<string, OpenApiExample>
                {
                    ["acme"] = new OpenApiExample
                    {
                        Summary = "Tenant ACME Corp",
                        Description = "Ejemplo de una empresa",
                        Value = new Microsoft.OpenApi.Any.OpenApiString("acme")
                    },
                    ["demo-store"] = new OpenApiExample
                    {
                        Summary = "Tienda Demo",
                        Description = "Ejemplo de tienda de demostración",
                        Value = new Microsoft.OpenApi.Any.OpenApiString("demo-store")
                    },
                    ["mi-tienda-123"] = new OpenApiExample
                    {
                        Summary = "Mi Tienda 123",
                        Description = "Ejemplo con números",
                        Value = new Microsoft.OpenApi.Any.OpenApiString("mi-tienda-123")
                    }
                }
            });

            // Opcionalmente agregar query parameter (menos común pero soportado)
            var hasQueryTenant = operation.Parameters.Any(p => 
                p.Name == "tenant" && p.In == ParameterLocation.Query);

            if (!hasQueryTenant)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "tenant",
                    In = ParameterLocation.Query,
                    Required = false,
                    Description = @"
**Alternativa al header X-Tenant-Slug** (query parameter)

?? **No recomendado**: Es preferible usar el header `X-Tenant-Slug`.

Este parámetro existe para casos donde no es posible enviar headers personalizados (ej: algunos webhooks, iframes, etc.).

**Uso:**
```
GET /api/catalog/products?tenant=acme
```

**Prioridad:**
1. Header `X-Tenant-Slug` (recomendado)
2. Query parameter `tenant` (fallback)

Si ambos están presentes, se usa el header.
",
                    Schema = new OpenApiSchema
                    {
                        Type = "string",
                        Example = new Microsoft.OpenApi.Any.OpenApiString("acme")
                    }
                });
            }
        }

        private static bool IsExcludedPath(string path)
        {
            return ExcludedPaths.Any(excluded => path.StartsWith(excluded));
        }
    }
}
