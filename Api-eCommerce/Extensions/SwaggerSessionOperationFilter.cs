using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api_eCommerce.Extensions
{
    /// <summary>
    /// Operation filter que agrega el header X-Session-Id para endpoints de carrito y checkout
    /// </summary>
    public class SwaggerSessionOperationFilter : IOperationFilter
    {
        // Rutas que requieren X-Session-Id
        private static readonly string[] SessionRequiredPaths = 
        {
            "/api/cart",
            "/api/checkout"
        };

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // Obtener la ruta del endpoint
            var path = context.ApiDescription.RelativePath?.ToLower() ?? string.Empty;

            // Verificar si la ruta requiere session
            if (!RequiresSession(path))
            {
                return;
            }

            // Inicializar parámetros si es null
            operation.Parameters ??= new List<OpenApiParameter>();

            // Agregar el header X-Session-Id
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Session-Id",
                In = ParameterLocation.Header,
                Required = true,
                Description = @"
Identificador único de sesión para operaciones de carrito y checkout.

**Formato:** String único (UUID recomendado)

**Ejemplo:** `sess_abc123def456` o `550e8400-e29b-41d4-a716-446655440000`

**Generación:**
- Frontend: Genera un UUID al cargar la aplicación y guárdalo en localStorage
- Mobile: Genera un UUID y guárdalo en preferencias/storage
- Backend: `Guid.NewGuid().ToString()`

**Ciclo de vida:**
- Se crea al iniciar una sesión de compra
- Persiste mientras el usuario navega (carrito, checkout)
- Se descarta después de completar la orden o cerrar sesión

**Nota:** Si no se proporciona, la API retornará 400 Bad Request.
",
                Schema = new OpenApiSchema
                {
                    Type = "string",
                    Example = new Microsoft.OpenApi.Any.OpenApiString("sess_abc123def456")
                }
            });
        }

        private static bool RequiresSession(string path)
        {
            return SessionRequiredPaths.Any(required => path.StartsWith(required));
        }
    }

    /// <summary>
    /// Operation filter que documenta respuestas HTTP comunes a todos los endpoints
    /// </summary>
    public class SwaggerCommonResponsesOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var path = context.ApiDescription.RelativePath?.ToLower() ?? string.Empty;
            var method = context.ApiDescription.HttpMethod?.ToUpper() ?? "GET";

            // ========================================
            // Respuestas comunes a todos los endpoints
            // ========================================

            // 500 - Internal Server Error (todos los endpoints)
            if (!operation.Responses.ContainsKey("500"))
            {
                operation.Responses.Add("500", new OpenApiResponse
                {
                    Description = @"
**Internal Server Error**

Error interno del servidor. Contacta al equipo de soporte si el error persiste.

**Posibles causas:**
- Error en la base de datos
- Error en un servicio externo
- Error no controlado en la lógica de negocio

**Respuesta típica:**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.6.1"",
  ""title"": ""An error occurred while processing your request."",
  ""status"": 500,
  ""traceId"": ""00-123abc...""
}
```
",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = new Dictionary<string, OpenApiSchema>
                                {
                                    ["type"] = new OpenApiSchema { Type = "string" },
                                    ["title"] = new OpenApiSchema { Type = "string" },
                                    ["status"] = new OpenApiSchema { Type = "integer" },
                                    ["traceId"] = new OpenApiSchema { Type = "string" }
                                }
                            },
                            Example = new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.6.1"),
                                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("An error occurred while processing your request."),
                                ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(500),
                                ["traceId"] = new Microsoft.OpenApi.Any.OpenApiString("00-123abc456def789-xyz-00")
                            }
                        }
                    }
                });
            }

            // ========================================
            // Respuestas para endpoints con tenant
            // ========================================

            if (RequiresTenant(path))
            {
                // 400 - Bad Request (tenant no proporcionado)
                if (!operation.Responses.ContainsKey("400"))
                {
                    operation.Responses.Add("400", new OpenApiResponse
                    {
                        Description = @"
**Bad Request**

La solicitud no es válida. Verifica los parámetros enviados.

**Causas comunes:**
- Header `X-Tenant-Slug` no proporcionado
- Header `X-Session-Id` no proporcionado (en carrito/checkout)
- Formato inválido en el body del request
- Parámetros obligatorios faltantes

**Ejemplo (tenant no proporcionado):**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.5.1"",
  ""title"": ""Bad Request"",
  ""status"": 400,
  ""detail"": ""X-Tenant-Slug header is required""
}
```
",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.1"),
                                    ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Bad Request"),
                                    ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(400),
                                    ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("X-Tenant-Slug header is required")
                                }
                            }
                        }
                    });
                }

                // 404 - Not Found (tenant no existe)
                if (!operation.Responses.ContainsKey("404"))
                {
                    operation.Responses.Add("404", new OpenApiResponse
                    {
                        Description = @"
**Not Found**

El recurso solicitado no fue encontrado.

**Causas comunes:**
- Tenant con el slug proporcionado no existe
- Recurso específico (producto, orden, etc.) no existe
- Ruta incorrecta

**Ejemplo (tenant no existe):**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.5.5"",
  ""title"": ""Not Found"",
  ""status"": 404,
  ""detail"": ""Tenant 'invalid-slug' not found""
}
```
",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.5"),
                                    ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Not Found"),
                                    ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(404),
                                    ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("Tenant 'invalid-slug' not found")
                                }
                            }
                        }
                    });
                }

                // 403 - Forbidden (tenant no activo)
                if (!operation.Responses.ContainsKey("403"))
                {
                    operation.Responses.Add("403", new OpenApiResponse
                    {
                        Description = @"
**Forbidden**

No tienes permiso para acceder a este recurso.

**Causas comunes:**
- Tenant no está en estado 'Ready' (puede estar 'Pending', 'Failed', 'Suspended')
- Feature flag deshabilitada para el tenant
- Quota excedida
- Autenticación requerida pero no proporcionada

**Ejemplo (tenant no activo):**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.5.4"",
  ""title"": ""Forbidden"",
  ""status"": 403,
  ""detail"": ""Tenant 'acme' is not active. Current status: Pending""
}
```

**Ejemplo (feature deshabilitada):**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.5.4"",
  ""title"": ""Feature disabled"",
  ""status"": 403,
  ""detail"": ""Feature 'wompiEnabled' is disabled for tenant 'acme'""
}
```
",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.4"),
                                    ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Forbidden"),
                                    ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(403),
                                    ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("Tenant is not active")
                                }
                            }
                        }
                    });
                }

                // 409 - Conflict (tenant en proceso o error)
                if (!operation.Responses.ContainsKey("409"))
                {
                    operation.Responses.Add("409", new OpenApiResponse
                    {
                        Description = @"
**Conflict**

Conflicto al procesar la solicitud.

**Causas comunes:**
- Tenant no resuelto o no listo
- Recurso duplicado
- Estado inconsistente

**Ejemplo:**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.5.10"",
  ""title"": ""Conflict"",
  ""status"": 409,
  ""detail"": ""Tenant not resolved or not ready""
}
```
",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.10"),
                                    ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Conflict"),
                                    ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(409),
                                    ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("Tenant not resolved or not ready")
                                }
                            }
                        }
                    });
                }
            }

            // ========================================
            // Respuestas para endpoints autenticados
            // ========================================

            if (RequiresAuth(context))
            {
                // 401 - Unauthorized
                if (!operation.Responses.ContainsKey("401"))
                {
                    operation.Responses.Add("401", new OpenApiResponse
                    {
                        Description = @"
**Unauthorized**

Autenticación requerida o token inválido.

**Causas comunes:**
- Token JWT no proporcionado
- Token JWT expirado
- Token JWT inválido o mal formado
- Credenciales incorrectas (en endpoints de login)
- Guest checkout deshabilitado y no hay JWT

**Ejemplo:**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc9110#section-15.5.2"",
  ""title"": ""Unauthorized"",
  ""status"": 401
}
```

**Solución:**
1. Obtén un token válido haciendo login en `/auth/login`
2. Incluye el token en el header: `Authorization: Bearer {token}`
",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Example = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://tools.ietf.org/html/rfc9110#section-15.5.2"),
                                    ["title"] = new Microsoft.OpenApi.Any.OpenApiString("Unauthorized"),
                                    ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(401)
                                }
                            }
                        }
                    });
                }
            }

            // ========================================
            // Respuestas específicas por método
            // ========================================

            // POST/PUT - 422 Unprocessable Entity
            if ((method == "POST" || method == "PUT" || method == "PATCH") && !operation.Responses.ContainsKey("422"))
            {
                operation.Responses.Add("422", new OpenApiResponse
                {
                    Description = @"
**Unprocessable Entity**

La solicitud está bien formada pero contiene errores de validación.

**Causas comunes:**
- Campos requeridos faltantes
- Formato inválido (email, teléfono, etc.)
- Valores fuera de rango
- Restricciones de negocio no cumplidas

**Ejemplo:**
```json
{
  ""type"": ""https://tools.ietf.org/html/rfc4918#section-11.2"",
  ""title"": ""One or more validation errors occurred."",
  ""status"": 422,
  ""errors"": {
    ""email"": [""The email field is required.""],
    ""password"": [""Password must be at least 8 characters.""]
  }
}
```
",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/json"] = new OpenApiMediaType
                        {
                            Example = new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["type"] = new Microsoft.OpenApi.Any.OpenApiString("https://tools.ietf.org/html/rfc4918#section-11.2"),
                                ["title"] = new Microsoft.OpenApi.Any.OpenApiString("One or more validation errors occurred."),
                                ["status"] = new Microsoft.OpenApi.Any.OpenApiInteger(422),
                                ["errors"] = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["email"] = new Microsoft.OpenApi.Any.OpenApiArray
                                    {
                                        new Microsoft.OpenApi.Any.OpenApiString("The email field is required.")
                                    }
                                }
                            }
                        }
                    }
                });
            }
        }

        private static bool RequiresTenant(string path)
        {
            var excludedPaths = new[] { "/swagger", "/health", "/provision", "/superadmin", "/public" };
            return !excludedPaths.Any(excluded => path.StartsWith(excluded));
        }

        private static bool RequiresAuth(OperationFilterContext context)
        {
            // Verificar si el endpoint tiene el atributo [Authorize]
            var hasAuthorize = context.MethodInfo
                .GetCustomAttributes(true)
                .Any(attr => attr.GetType().Name == "AuthorizeAttribute");

            return hasAuthorize;
        }
    }
}
