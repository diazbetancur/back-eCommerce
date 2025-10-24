using Api_eCommerce.Endpoints;
using Api_eCommerce.Handlers;
using Api_eCommerce.Metering;
using Api_eCommerce.Middleware;
using CC.Domain.Entities;
using CC.Infraestructure.Admin;
using CC.Infraestructure.Configurations;
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

builder.Services.AddDbContext<AdminDbContext>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("ADMIN_PG")));
builder.Services.AddDataProtection();
builder.Services.AddScoped<ITenantConnectionProtector, TenantConnectionProtector>();
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddSingleton<TenantDbContextFactory>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

#region Swagger

SwaggerHandler.SwaggerConfig(builder.Services);

# endregion

#region Register (dependency injection)

DependencyInyectionHandler.DepencyInyectionConfig(builder.Services);

#endregion Register (dependency injection)

#region IdentityCore

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

#endregion IdentityCore

# region JWT
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

#endregion Swagger

var app = builder.Build();

//SeedData(app);

void SeedData(WebApplication app)
{
    // Solo se usa si trabajamos con una semilla de datos
    //var scopedFactory = app.Services.GetService<IServiceScopeFactory>();
    //using var scope = scopedFactory!.CreateScope();
    //var service = scope.ServiceProvider.GetService<SeedDB>();
    //service!.SeedAsync().Wait();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseMiddleware<MeteringMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseHttpsRedirection();
app.UseMiddleware(typeof(ErrorHandlingMiddleware));
app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.MapSuperAdminTenants();
app.MapPublicTenantConfig();

app.Run();