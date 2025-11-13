using Api_eCommerce.Auth;
using Api_eCommerce.Endpoints;
using Api_eCommerce.Extensions;
using Api_eCommerce.Handlers;
using Api_eCommerce.Metering;
using Api_eCommerce.Middleware;
using Api_eCommerce.Workers;
using CC.Aplication.Services;
using CC.Domain.Entities;
using CC.Infraestructure.Admin; // ? CORRECTO: Usar Admin en lugar de AdminDb
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
// Servicios de multi-tenancy
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
// Servicios para aprovisionamiento de tenants
builder.Services.AddScoped<IConfirmTokenService, ConfirmTokenService>();
builder.Services.AddScoped<ITenantDatabaseCreator, TenantDatabaseCreator>();
builder.Services.AddScoped<ITenantProvisioner, TenantProvisioner>();

// Worker para procesamiento en background
builder.Services.AddSingleton<TenantProvisioningWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TenantProvisioningWorker>());
#endregion

#region Business Services
// Servicios de negocio del tenant
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
#endregion

#region Swagger Configuration
// Configuración de Swagger con soporte multi-tenant
builder.Services.AddMultiTenantSwagger();
#endregion

#region Register (dependency injection)
DependencyInyectionHandler.DepencyInyectionConfig(builder.Services);
#endregion

#region IdentityCore
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

#region JWT
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

// Middlewares - ORDEN IMPORTANTE
app.UseHttpsRedirection();

// 1. Metering debe ir primero para capturar todas las requests
app.UseMiddleware<MeteringMiddleware>();

// 2. Tenant resolution - resuelve el tenant antes de autenticación
app.UseMiddleware<TenantResolutionMiddleware>();

// 3. Error handling
app.UseMiddleware(typeof(ErrorHandlingMiddleware));

// 4. Autenticación y autorización
app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();

// Mapear controladores y endpoints
app.MapControllers();

// Endpoints de administración
app.MapSuperAdminTenants();
app.MapPublicTenantConfig();
app.MapProvisioningEndpoints();
app.MapFeatureFlagsEndpoints();

// Endpoints de negocio (requieren tenant)
app.MapCatalogEndpoints();
app.MapCartEndpoints();
app.MapCheckoutEndpoints();

app.Run();