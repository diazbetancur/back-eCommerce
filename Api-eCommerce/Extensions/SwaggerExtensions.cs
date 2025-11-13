using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace Api_eCommerce.Extensions
{
    /// <summary>
    /// Extensiones para configurar Swagger en la aplicación multi-tenant
    /// </summary>
    public static class SwaggerExtensions
    {
        /// <summary>
        /// Configura Swagger con soporte completo para multi-tenant
        /// </summary>
        public static IServiceCollection AddMultiTenantSwagger(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            
            services.AddSwaggerGen(options =>
            {
                // ========================================
                // Configuración de documentos Swagger
                // ========================================
                
                // Documento principal - API Multi-Tenant
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "eCommerce Multi-Tenant API",
                    Version = "v1.0.0",
                    Description = @"
## ?? API Multi-Tenant para eCommerce

Esta API soporta **múltiples tenants** (inquilinos) aislados, cada uno con su propia base de datos y configuración.

### ?? Características principales:

- **Multi-tenancy**: Cada tenant tiene datos completamente aislados
- **Provisioning automático**: Creación de tenants con base de datos dedicada
- **Feature flags**: Configuración de features por tenant y plan
- **Metering**: Monitoreo de uso y quotas
- **Checkout**: Carrito, cotización y órdenes
- **Catálogo**: Productos, categorías y búsqueda

### ?? Autenticación:

La API usa **JWT Bearer tokens** para autenticación. Para endpoints que requieren autenticación:

1. Obtén un token haciendo login en `/auth/login`
2. Incluye el token en el header: `Authorization: Bearer {token}`

### ??? Headers requeridos:

#### X-Tenant-Slug (requerido para la mayoría de endpoints)
Identifica el tenant al que pertenece la solicitud. Ejemplos: `acme`, `demo-store`, `mi-tienda`

#### X-Session-Id (requerido para carrito/checkout)
Identificador único de sesión para operaciones de carrito. Genera un UUID: `sess_abc123def456`

### ?? Documentación adicional:

- Guía de inicio rápido: Ver `FEATURE-FLAGS-QUICKSTART.md`
- Ejemplos de API: Ver `FEATURE-FLAGS-API-EXAMPLES.md`
",
                    Contact = new OpenApiContact
                    {
                        Name = "eCommerce API Support",
                        Email = "support@example.com",
                        Url = new Uri("https://example.com/support")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });

                // Documento SuperAdmin - Endpoints de administración
                options.SwaggerDoc("superadmin", new OpenApiInfo
                {
                    Title = "SuperAdmin API",
                    Version = "v1.0.0",
                    Description = @"
## ?? API de Administración SuperAdmin

Endpoints para administración de tenants, planes, features y configuración global.

### ?? Acceso restringido:

Estos endpoints están destinados **solo para administradores del sistema**.
En producción, estos endpoints deben estar protegidos por autenticación y autorización específica.

### ?? Funcionalidades:

- Gestión de tenants (crear, actualizar, eliminar)
- Configuración de feature flags
- Gestión de planes y quotas
- Monitoreo de uso y métricas
"
                });

                // Documento de Provisioning - Registro de nuevos tenants
                options.SwaggerDoc("provisioning", new OpenApiInfo
                {
                    Title = "Provisioning API",
                    Version = "v1.0.0",
                    Description = @"
## ?? API de Aprovisionamiento de Tenants

Endpoints públicos para registro y activación de nuevos tenants.

### ?? Flujo de provisioning:

1. **Iniciar registro**: `POST /provision/tenants/init`
   - Proporciona información básica del tenant
   - Recibe email de confirmación

2. **Confirmar registro**: `POST /provision/tenants/confirm`
   - Confirma con el token recibido por email
   - Se crea la base de datos y configuración inicial

3. **Tenant listo**: El tenant queda en estado `Ready`
   - Ya puede usar todos los endpoints de la API
"
                });

                // ========================================
                // Seguridad - JWT Bearer
                // ========================================
                
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = @"
### Autenticación JWT

Ingresa tu token JWT en el campo de texto.

**Formato:** `Bearer {token}`

**Ejemplo:**
```
Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Cómo obtener un token:

1. Haz login en `/auth/login` con tu email y contraseña
2. Copia el token de la respuesta
3. Haz clic en el botón **Authorize** en Swagger
4. Pega el token (sin el prefijo 'Bearer ')
"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // ========================================
                // Filtros de operación
                // ========================================
                
                // Agregar header X-Tenant-Slug automáticamente
                options.OperationFilter<SwaggerTenantOperationFilter>();
                
                // Agregar header X-Session-Id para endpoints de carrito/checkout
                options.OperationFilter<SwaggerSessionOperationFilter>();
                
                // Documentar respuestas comunes (400, 401, 404, 500)
                options.OperationFilter<SwaggerCommonResponsesOperationFilter>();
                
                // Agrupar endpoints por dominio
                options.TagActionsBy(api =>
                {
                    var controllerName = api.ActionDescriptor.RouteValues["controller"];
                    var relativePath = api.RelativePath?.ToLower() ?? "";

                    // Agrupar por ruta
                    if (relativePath.StartsWith("superadmin/"))
                        return new[] { "SuperAdmin" };
                    if (relativePath.StartsWith("provision/"))
                        return new[] { "Provisioning" };
                    if (relativePath.StartsWith("public/"))
                        return new[] { "Public" };
                    if (relativePath.StartsWith("health"))
                        return new[] { "Health" };
                    if (relativePath.StartsWith("auth/"))
                        return new[] { "Authentication" };
                    if (relativePath.StartsWith("api/catalog"))
                        return new[] { "Catalog" };
                    if (relativePath.StartsWith("api/cart"))
                        return new[] { "Cart" };
                    if (relativePath.StartsWith("api/checkout"))
                        return new[] { "Checkout" };
                    if (relativePath.StartsWith("api/features"))
                        return new[] { "Features" };

                    return new[] { controllerName ?? "Other" };
                });

                // ========================================
                // Documentación XML
                // ========================================
                
                // Incluir comentarios XML si existen
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }

                // ========================================
                // Configuración adicional
                // ========================================
                
                // Ordenar acciones por método HTTP
                options.OrderActionsBy(api => 
                    $"{api.GroupName}_{api.HttpMethod}_{api.RelativePath}");

                // Mostrar enums como strings
                options.SchemaFilter<EnumSchemaFilter>();
                
                // Ejemplos de request/response
                options.SchemaFilter<ExampleSchemaFilter>();
            });

            return services;
        }

        /// <summary>
        /// Configura el UI de Swagger con opciones personalizadas
        /// </summary>
        public static IApplicationBuilder UseMultiTenantSwaggerUI(this IApplicationBuilder app)
        {
            app.UseSwagger(options =>
            {
                options.RouteTemplate = "swagger/{documentName}/swagger.json";
            });

            app.UseSwaggerUI(options =>
            {
                // Documentos disponibles
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "eCommerce API v1");
                options.SwaggerEndpoint("/swagger/superadmin/swagger.json", "SuperAdmin API");
                options.SwaggerEndpoint("/swagger/provisioning/swagger.json", "Provisioning API");

                // Configuración UI
                options.DocumentTitle = "eCommerce Multi-Tenant API";
                options.RoutePrefix = "swagger";
                
                // Habilitar búsqueda
                options.EnableFilter();
                
                // Expandir operaciones por defecto
                options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
                
                // Mostrar duraciones de request
                options.DisplayRequestDuration();
                
                // Tema oscuro
                options.DefaultModelsExpandDepth(2);
                options.DefaultModelExpandDepth(2);
                
                // Persistir autenticación
                options.EnableDeepLinking();
                options.DisplayOperationId();
                
                // Configuración personalizada
                options.InjectStylesheet("/swagger-custom.css");
                options.InjectJavascript("/swagger-custom.js");
            });

            return app;
        }
    }

    /// <summary>
    /// Filtro para mostrar enums como strings en lugar de números
    /// </summary>
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                schema.Enum.Clear();
                foreach (var name in Enum.GetNames(context.Type))
                {
                    schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(name));
                }
                schema.Type = "string";
                schema.Format = null;
            }
        }
    }

    /// <summary>
    /// Filtro para agregar ejemplos a los schemas
    /// </summary>
    public class ExampleSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            // Aquí puedes agregar ejemplos personalizados para tus DTOs
            // Por ejemplo, para ProductDto, CheckoutRequest, etc.
        }
    }
}
