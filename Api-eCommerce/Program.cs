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
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

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

// ==================== BUSINESS SERVICES (TENANT) ====================
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IFeatureService, FeatureService>();
builder.Services.AddScoped<CC.Aplication.Auth.IAuthService, CC.Aplication.Auth.AuthService>();
builder.Services.AddScoped<CC.Aplication.Orders.IOrderService, CC.Aplication.Orders.OrderService>();
builder.Services.AddScoped<CC.Aplication.Favorites.IFavoritesService, CC.Aplication.Favorites.FavoritesService>();
builder.Services.AddScoped<CC.Aplication.Loyalty.ILoyaltyService, CC.Aplication.Loyalty.LoyaltyService>();

// ==================== SWAGGER ====================
builder.Services.AddMultiTenantSwagger();

// ==================== LEGACY DI (Revisar y limpiar) ====================
DependencyInyectionHandler.DepencyInyectionConfig(builder.Services);

// ==================== JWT AUTHENTICATION ====================
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

var app = builder.Build();

// ==================== AUTO MIGRATE + SEED ====================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("?? Applying AdminDb migrations...");
        await adminDb.Database.MigrateAsync();  // ? AUTO MIGRATE
        
        logger.LogInformation("?? Seeding AdminDb...");
        await CC.Infraestructure.Admin.AdminDbSeeder.SeedAsync(adminDb, logger);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "? Error during startup migration/seed");
        throw; // ? Fail fast en MVP
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMultiTenantSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<MeteringMiddleware>();
app.UseMiddleware(typeof(ErrorHandlingMiddleware));
app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();

// ==================== ROUTES ====================
app.MapAdminEndpoints();
app.MapProvisioningEndpoints();
app.MapSuperAdminTenants();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .AllowAnonymous();

app.MapPublicTenantConfig();

var tenantGroup = app.MapGroup("").RequireTenantResolution();
tenantGroup.MapGroup("").MapFeatureFlagsEndpoints();
tenantGroup.MapGroup("").MapTenantAuth();
tenantGroup.MapGroup("").MapCatalogEndpoints();
tenantGroup.MapGroup("").MapCartEndpoints();
tenantGroup.MapGroup("").MapCheckoutEndpoints();
tenantGroup.MapGroup("").MapOrdersEndpoints();
tenantGroup.MapGroup("").MapFavoritesEndpoints();
tenantGroup.MapGroup("").MapLoyaltyEndpoints();

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

            if (httpContext.Response.HasStarted)
            {
                return null;
            }

            return await next(context);
        });
    }
}