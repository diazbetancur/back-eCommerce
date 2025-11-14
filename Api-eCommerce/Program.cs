using Api_eCommerce.Auth;
using Api_eCommerce.Endpoints;
using Api_eCommerce.Extensions;
using Api_eCommerce.Handlers;
using Api_eCommerce.Metering;
using Api_eCommerce.Middleware;
using Api_eCommerce.Workers;
using CC.Aplication.Services;
using CC.Domain.Entities;
using CC.Infraestructure.Admin;
using CC.Infraestructure.Configurations;
using CC.Infraestructure.EF;
using CC.Infraestructure.Provisioning;
using CC.Infraestructure.Sql;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

#region Admin DB Configuration
// Configuración de Admin DB - Base de datos central para gestión de tenants
builder.Services.AddDbContext<AdminDbContext>(opt => 
    opt.UseNpgsql(builder.Configuration.GetConnectionString("AdminDb"),
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "admin")));
#endregion

#region Legacy DB Context Configuration
// ?? LEGACY: DBContext para compatibilidad con código anterior (Identity)
// Este contexto usa la base de datos PgSQL configurada
builder.Services.AddDbContext<DBContext>(opt => 
    opt.UseNpgsql(builder.Configuration.GetConnectionString("PgSQL")));
#endregion

#region Caching
// Memory cache para feature flags y otros
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CC.Infraestructure.Cache.IFeatureCache, CC.Infraestructure.Cache.FeatureCache>();
#endregion

#region Tenancy Services
// Servicios de multi-tenancy (SOLO para rutas tenant-scoped)
builder.Services.AddScoped<ITenantAccessor, TenantAccessor>();
builder.Services.AddScoped<TenantDbContextFactory>();

// Legacy services (mantener por compatibilidad si es necesario)
builder.Services.AddDataProtection();
builder.Services.AddScoped<ITenantConnectionProtector, TenantConnectionProtector>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
#endregion

#region EF Core Services
// Servicios para migraciones EF Core
builder.Services.AddScoped<IMigrationRunner, MigrationRunner>();
#endregion

#region Provisioning Services
// Servicios para aprovisionamiento de tenants (usan AdminDb)
builder.Services.AddScoped<IConfirmTokenService, ConfirmTokenService>();
builder.Services.AddScoped<ITenantDatabaseCreator, TenantDatabaseCreator>();
builder.Services.AddScoped<ITenantProvisioner, TenantProvisioner>();

// Worker para procesamiento en background
builder.Services.AddSingleton<TenantProvisioningWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TenantProvisioningWorker>());
#endregion

#region Admin Services (NUEVO - SOLO AdminDb)
// Servicios administrativos - NO usan TenantDbContext
builder.Services.AddScoped<CC.Aplication.Admin.IAdminAuthService, CC.Aplication.Admin.AdminAuthService>();
builder.Services.AddScoped<CC.Aplication.Admin.IAdminTenantService, CC.Aplication.Admin.AdminTenantService>();
// TODO: Agregar AdminPlanService, AdminUserService, etc.
#endregion

#region Business Services (Tenant-Scoped)
// Servicios de negocio del tenant (SOLO para rutas con X-Tenant-Slug)
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IFeatureService, FeatureService>();

// Servicio de autenticación de usuarios por tenant
builder.Services.AddScoped<CC.Aplication.Auth.IAuthService, CC.Aplication.Auth.AuthService>();

// Servicio de órdenes de usuarios
builder.Services.AddScoped<CC.Aplication.Orders.IOrderService, CC.Aplication.Orders.OrderService>();

// Servicio de favoritos (wishlist) de usuarios
builder.Services.AddScoped<CC.Aplication.Favorites.IFavoritesService, CC.Aplication.Favorites.FavoritesService>();

// Servicio de fidelización (loyalty points) de usuarios
builder.Services.AddScoped<CC.Aplication.Loyalty.ILoyaltyService, CC.Aplication.Loyalty.LoyaltyService>();
#endregion

#region Swagger Configuration
// Configuración de Swagger con soporte multi-tenant
builder.Services.AddMultiTenantSwagger();
#endregion

#region Register (dependency injection)
DependencyInyectionHandler.DepencyInyectionConfig(builder.Services);
#endregion

#region IdentityCore (Legacy)
// ?? NOTA: Identity está configurado para usar DBContext legacy
// En el futuro, esto debe migrarse a usar TenantDbContext por tenant
builder.Services.AddIdentity<User, Role>(opt =>
{
    opt.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
    opt.SignIn.RequireConfirmedEmail = false;
    opt.Password.RequiredLength = 8;
    opt.Password.RequireLowercase = true;
    opt.Password.RequireUppercase = true;
    opt.Password.RequireNonAlphanumeric = true;
    opt.Password.RequiredUniqueChars = 1;
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
}).AddRoles<Role>().AddEntityFrameworkStores<DBContext>().AddDefaultTokenProviders();
#endregion

#region JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(x => x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(builder.Configuration["jwtKey"]!)),
        ClockSkew = TimeSpan.Zero
    });
#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMultiTenantSwaggerUI();
}
else
{
    app.UseHsts();
}

// ==================== GLOBAL MIDDLEWARES ====================
app.UseHttpsRedirection();

// 1. Metering debe ir primero para capturar todas las requests
app.UseMiddleware<MeteringMiddleware>();

// 2. Error handling global
app.UseMiddleware(typeof(ErrorHandlingMiddleware));

// 3. Autenticación y autorización (global)
app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();

// ==================== ADMIN ROUTES (NO TENANT) ====================
// Estos endpoints NO requieren X-Tenant-Slug y usan SOLO AdminDb
var adminGroup = app.MapGroup("/admin")
    .WithTags("Administration");

// Admin endpoints
app.MapAdminEndpoints();

// Provisioning endpoints (usan AdminDb)
app.MapProvisioningEndpoints();

// SuperAdmin endpoints (usan AdminDb)
app.MapSuperAdminTenants();

// ==================== GLOBAL/PUBLIC ROUTES (NO TENANT) ====================
// Health check global
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .AllowAnonymous();

// Public tenant config (usa AdminDb para buscar tenant)
app.MapPublicTenantConfig();

// ==================== TENANT-SCOPED ROUTES ====================
// Estos endpoints REQUIEREN X-Tenant-Slug y usan TenantDbContext
var tenantGroup = app.MapGroup("")
    .RequireTenantResolution(); // ? Middleware personalizado

// Mapear endpoints de tenant
tenantGroup.MapGroup("").MapFeatureFlagsEndpoints();
tenantGroup.MapGroup("").MapTenantAuth();
tenantGroup.MapGroup("").MapCatalogEndpoints();
tenantGroup.MapGroup("").MapCartEndpoints();
tenantGroup.MapGroup("").MapCheckoutEndpoints();
tenantGroup.MapGroup("").MapOrdersEndpoints();
tenantGroup.MapGroup("").MapFavoritesEndpoints();
tenantGroup.MapGroup("").MapLoyaltyEndpoints();

// Legacy controllers (revisar cuáles son tenant-scoped)
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
            
            // Aplicar TenantResolutionMiddleware manualmente
            var middleware = new TenantResolutionMiddleware(
                async (ctx) => { await Task.CompletedTask; },
                httpContext.RequestServices.GetRequiredService<ILogger<TenantResolutionMiddleware>>()
            );

            var adminDb = httpContext.RequestServices.GetRequiredService<AdminDbContext>();
            var tenantAccessor = httpContext.RequestServices.GetRequiredService<ITenantAccessor>();
            var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();

            await middleware.InvokeAsync(httpContext, adminDb, tenantAccessor, configuration);

            // Si TenantResolution falló, ya se escribió la respuesta
            if (httpContext.Response.HasStarted)
            {
                return null;
            }

            // Continuar con el endpoint
            return await next(context);
        });
    }
}