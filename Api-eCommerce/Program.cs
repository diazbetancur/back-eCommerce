using Api_eCommerce.Handlers;
using CC.Domain.Entities;
using CC.Infraestructure.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddControllers().AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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

    #region Headers

    app.Use(async (context, next) =>
    {
        context.Response.Headers.Clear();

        context.Response.Headers.Add("Content-Security-Policy",
            "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; img-src 'self' data:");
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-Permitted-Cross-Domain-Policies", "master-only");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Add("Cache-Control", "no-cache,no-store,must-revalidate");
        context.Response.Headers.Add("Pragma", "no-cache");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Add("Referrer-Policy", "no-referrer");
        context.Response.Headers.Add("Permissions-Policy", "fullscreen=(), geolocation=()");
        context.Request.Headers.Add("X-Content-Type-Options", "nosniff");

        await next();
    });

    #endregion Headers
}

app.UseCors(x => x
.AllowAnyMethod()
.AllowAnyHeader()
.SetIsOriginAllowed(origin => true)
.AllowCredentials());

app.UseHttpsRedirection();
app.UseMiddleware(typeof(ErrorHandlingMiddleware));
app.UseAuthentication();
app.UseMiddleware<ActivityLoggingMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();