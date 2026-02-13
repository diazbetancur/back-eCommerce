using Api_eCommerce.Auth;
using Api_eCommerce.Endpoints;
using Api_eCommerce.Extensions;
using Api_eCommerce.Handlers;
using Api_eCommerce.Metering;
using Api_eCommerce.Middleware;
using Api_eCommerce.Workers;
using CC.Aplication.Services;
using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin;
using CC.Infraestructure.EF;
using CC.Infraestructure.Provisioning;
using CC.Infraestructure.Sql;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ==================== ACTION FILTERS ====================
builder.Services.AddScoped<Api_eCommerce.Authorization.ModuleAuthorizationActionFilter>();

builder.Services.AddControllers();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// ==================== CORS ====================
// NOTA: Configuración temporal - Permite cualquier dominio para testing
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()      // ⚠️ TEMPORAL: Permite cualquier dominio
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Tenant-Slug");  // Exponer header custom
        // NOTA: AllowCredentials() no se puede usar con AllowAnyOrigin()
    });
});

// ==================== ADMIN DB ====================
builder.Services.AddDbContext<AdminDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("AdminDb"),
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "admin")));

// ==================== CACHING ====================
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CC.Infraestructure.Cache.IFeatureCache, CC.Infraestructure.Cache.FeatureCache>();

// ==================== TENANCY SERVICES ====================
builder.Services.AddScoped<ITenantAccessor, TenantAccessor>();
builder.Services.AddScoped<TenantDbContextFactory>();
builder.Services.AddScoped<ITenantUnitOfWorkFactory, TenantUnitOfWorkFactory>();
builder.Services.AddDataProtection();
builder.Services.AddScoped<ITenantConnectionProtector, TenantConnectionProtector>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();

// ==================== EF CORE SERVICES ====================
builder.Services.AddScoped<IMigrationRunner, MigrationRunner>();

// ==================== PROVISIONING SERVICES ====================
builder.Services.AddScoped<IConfirmTokenService, ConfirmTokenService>();
builder.Services.AddScoped<ITenantDatabaseCreator, TenantDatabaseCreator>();
builder.Services.AddScoped<ITenantProvisioner, TenantProvisioner>();
builder.Services.AddSingleton<TenantProvisioningWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TenantProvisioningWorker>());

// ==================== ADMIN SERVICES ====================
builder.Services.AddScoped<CC.Aplication.Admin.IAdminAuthService, CC.Aplication.Admin.AdminAuthService>();
builder.Services.AddScoped<CC.Aplication.Admin.IAdminTenantService, CC.Aplication.Admin.AdminTenantService>();
builder.Services.AddScoped<CC.Aplication.Roles.IRoleService, CC.Aplication.Roles.RoleService>();
builder.Services.AddScoped<CC.Aplication.Users.IUserManagementService, CC.Aplication.Users.UserManagementService>();

// ==================== BUSINESS SERVICES (TENANT) ====================
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.AddScoped<CC.Aplication.Catalog.ICategoryManagementService, CC.Aplication.Catalog.CategoryManagementService>();
builder.Services.AddScoped<CC.Aplication.Catalog.IProductService, CC.Aplication.Catalog.ProductService>();

// Auth services
// ? DEPRECATED - Ahora usamos UnifiedAuthService
// builder.Services.AddScoped<CC.Aplication.Auth.IAuthService, CC.Aplication.Auth.AuthService>();
// builder.Services.AddScoped<CC.Aplication.TenantAuth.ITenantAuthService, CC.Aplication.TenantAuth.TenantAuthService>();

// ? NUEVO: Servicio unificado de autenticaci�n
builder.Services.AddScoped<CC.Aplication.Auth.IUnifiedAuthService, CC.Aplication.Auth.UnifiedAuthService>();

// Permission service
builder.Services.AddScoped<CC.Aplication.Permissions.IPermissionService, CC.Aplication.Permissions.PermissionService>();

// Plan Limit service ? NUEVO
builder.Services.AddScoped<CC.Aplication.Plans.IPlanLimitService, CC.Aplication.Plans.PlanLimitService>();

// Other services
builder.Services.AddScoped<CC.Aplication.Orders.IOrderService, CC.Aplication.Orders.OrderService>();
builder.Services.AddScoped<CC.Aplication.Favorites.IFavoritesService, CC.Aplication.Favorites.FavoritesService>();
builder.Services.AddScoped<CC.Aplication.Loyalty.ILoyaltyService, CC.Aplication.Loyalty.LoyaltyService>();
builder.Services.AddScoped<CC.Aplication.Loyalty.ILoyaltyRewardsService, CC.Aplication.Loyalty.LoyaltyRewardsService>();

// Store services (multi-location inventory)
builder.Services.AddScoped<CC.Aplication.Stores.IStoreService, CC.Aplication.Stores.StoreService>();
builder.Services.AddScoped<CC.Aplication.Stores.IStockService, CC.Aplication.Stores.StockService>();

// ==================== SWAGGER ====================
builder.Services.AddMultiTenantSwagger();

// ==================== LEGACY DI (Revisar y limpiar) ====================
DependencyInyectionHandler.DepencyInyectionConfig(builder.Services);

// ==================== JWT AUTHENTICATION ====================
var jwtStrictValidation = builder.Configuration.GetValue<bool>("Jwt:StrictValidation", true);
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? builder.Configuration["jwtKey"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ecommerce-api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ecommerce-clients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(x => x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = jwtStrictValidation,
        ValidIssuer = jwtIssuer,
        ValidateAudience = jwtStrictValidation,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSigningKey)),
        ClockSkew = TimeSpan.Zero
    });

// Log JWT configuration for debugging (only in Development)
if (builder.Environment.IsDevelopment())
{
    Console.WriteLine($"[JWT Config] StrictValidation: {jwtStrictValidation}");
    Console.WriteLine($"[JWT Config] Issuer: {jwtIssuer}");
    Console.WriteLine($"[JWT Config] Audience: {jwtAudience}");
}

var app = builder.Build();

// ==================== AUTO MIGRATE + SEED ====================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        // En Aiven Cloud, necesitamos crear la DB usando 'defaultdb' como base de mantenimiento
        var adminCs = config.GetConnectionString("AdminDb");
        var csb = new NpgsqlConnectionStringBuilder(adminCs);
        var targetDb = csb.Database;

        // Conectar a 'defaultdb' (base por defecto en Aiven) para crear la DB si no existe
        csb.Database = "defaultdb";
        var maintenanceCs = csb.ToString();

        try
        {
            await using var conn = new NpgsqlConnection(maintenanceCs);
            await conn.OpenAsync();

            // Verificar si la base de datos existe
            await using var checkCmd = new NpgsqlCommand(
                $"SELECT 1 FROM pg_database WHERE datname = '{targetDb}'", conn);
            var exists = await checkCmd.ExecuteScalarAsync() != null;

            if (!exists)
            {
                logger.LogInformation("📦 Creating database {Database}...", targetDb);
                await using var createCmd = new NpgsqlCommand(
                    $"CREATE DATABASE \"{targetDb}\" WITH ENCODING 'UTF8'", conn);
                await createCmd.ExecuteNonQueryAsync();
                logger.LogInformation("✅ Database {Database} created", targetDb);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("⚠️ Could not check/create database (may already exist): {Message}", ex.Message);
        }

        logger.LogInformation("🔄 Applying AdminDb migrations...");
        await adminDb.Database.MigrateAsync();

        logger.LogInformation("🌱 Seeding AdminDb...");
        await CC.Infraestructure.Admin.AdminDbSeeder.SeedAsync(adminDb, logger);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Error during startup migration/seed");
        throw;
    }
}

// ==================== HTTP PIPELINE ====================
// ⚠️ IMPORTANTE: CORS debe ir PRIMERO para manejar preflight requests
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseMultiTenantSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// ? Routing después de CORS
app.UseRouting();

// ? Autenticación antes de cualquier lógica de negocio
app.UseAuthentication();

// ? Middlewares de tenant ENTRE Authentication y Authorization
// para leer JWT y validar tenant antes de autorización
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseTenantUserOwnershipValidation();

// ? Autorización después de validar tenant
app.UseAuthorization();

// ? Middlewares personalizados DESPUÉS de autorización
// para evitar interferencia con peticiones OPTIONS de CORS
app.UseMiddleware<MeteringMiddleware>();
app.UseMiddleware(typeof(ErrorHandlingMiddleware));
app.UseMiddleware<ActivityLoggingMiddleware>();

// ==================== ROUTES ====================
app.MapAdminEndpoints();
app.MapRoleAdminEndpoints();
app.MapProvisioningEndpoints();
app.MapSuperAdminTenants();
app.MapSuperAdminPlans();  // ? Endpoint de planes (solo lectura)

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .AllowAnonymous();

app.MapPublicTenantConfig();

// ==================== STOREFRONT ENDPOINTS (Public) ====================
// ✅ MIGRADO: StorefrontController (público)
// Endpoints públicos del catálogo que requieren X-Tenant-Slug pero NO autenticación
// var storefrontGroup = app.MapGroup("");
// storefrontGroup.MapGroup("").MapStorefrontEndpoints();

// ==================== TENANT ENDPOINTS ====================
// NOTA: El middleware TenantResolutionMiddleware ya se ejecuta globalmente
var tenantGroup = app.MapGroup("");
tenantGroup.MapGroup("").MapFeatureFlagsEndpoints();
tenantGroup.MapGroup("").MapTenantAuth();
// tenantGroup.MapGroup("").MapPermissionsEndpoints();  // ✅ MIGRADO: PermissionsController
tenantGroup.MapGroup("").MapTenantAdminEndpoints(); // Ya incluye /admin/products
// tenantGroup.MapGroup("").MapCatalogEndpoints();  // ✅ MIGRADO: ProductController (público)
// tenantGroup.MapGroup("").MapCategoryEndpoints();  // ✅ MIGRADO: CategoryController
// tenantGroup.MapGroup("").MapProductEndpoints();  // ✅ MIGRADO: ProductAdminController
// tenantGroup.MapGroup("").MapCartEndpoints();  // ✅ MIGRADO: CartController
// tenantGroup.MapGroup("").MapCheckoutEndpoints();  // ✅ MIGRADO: CheckoutController
// tenantGroup.MapGroup("").MapOrdersEndpoints();  // ✅ MIGRADO: OrdersController
// tenantGroup.MapGroup("").MapFavoritesEndpoints();  // ✅ MIGRADO: FavoritesController
// tenantGroup.MapGroup("").MapLoyaltyEndpoints();  // ✅ MIGRADO: LoyaltyController

// ==================== CONTROLLERS ====================
// Los controllers ahora pasan por TenantResolutionMiddleware (agregado globalmente)
app.MapControllers();

app.Run();

// ==================== EXTENSION METHOD ====================
public static class TenantMiddlewareExtensions
{
    public static RouteGroupBuilder RequireTenantResolution(this RouteGroupBuilder group)
    {
        return group.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;

            var middleware = new TenantResolutionMiddleware(
                async (ctx) => { await Task.CompletedTask; },
                httpContext.RequestServices.GetRequiredService<ILogger<TenantResolutionMiddleware>>()
            );

            var adminDb = httpContext.RequestServices.GetRequiredService<AdminDbContext>();
            var tenantAccessor = httpContext.RequestServices.GetRequiredService<ITenantAccessor>();
            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

            await middleware.InvokeAsync(httpContext, adminDb, tenantAccessor, configuration);

            // Si el middleware escribió una respuesta de error (HasStarted = true),
            // no continuamos al siguiente handler. El filtro debe retornar algo,
            // pero como la respuesta ya fue escrita, retornamos el status actual
            if (httpContext.Response.HasStarted)
            {
                // La respuesta ya fue escrita por el middleware (error de tenant)
                // Simplemente retornamos sin invocar el handler del endpoint
                return TypedResults.StatusCode(httpContext.Response.StatusCode);
            }

            // Tenant resuelto correctamente, continuar con el endpoint
            return await next(context);
        });
    }
}
// Make Program class accessible for testing
public partial class Program { }
