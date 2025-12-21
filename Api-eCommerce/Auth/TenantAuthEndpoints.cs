using Api_eCommerce.Auth;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api_eCommerce.Auth
{
    public static class TenantAuthEndpoints
    {
        public static IEndpointRouteBuilder MapTenantAuth(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/auth");
            group.MapPost("/login", Login);
            group.MapPost("/change-password", ChangePassword).RequireAuthorization();
            return app;
        }

        private static async Task<IResult> Login(HttpContext http, ITenantResolver resolver, TenantDbContextFactory factory, IPasswordHasher hasher, ITokenService tokens, string email, string password)
        {
            var ctx = await resolver.ResolveAsync(http);
            if (ctx == null) return Results.Problem(statusCode: 409, detail: "Tenant not resolved or not ready");
            await using var db = factory.Create(ctx.ConnectionString);
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);
            if (user == null || !hasher.Verify(user.PasswordHash, password)) return Results.Unauthorized();
            var expires = DateTime.UtcNow.AddMinutes(60);
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email), new Claim("tenant_slug", ctx.Slug) };
            var token = tokens.CreateToken(claims, expires);
            return Results.Ok(new { token, expiresAt = expires });
        }

        private static async Task<IResult> ChangePassword(HttpContext http, ITenantResolver resolver, TenantDbContextFactory factory, IPasswordHasher hasher, string currentPassword, string newPassword)
        {
            var ctx = await resolver.ResolveAsync(http);
            if (ctx == null) return Results.Problem(statusCode: 409, detail: "Tenant not resolved or not ready");
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();
            await using var db = factory.Create(ctx.ConnectionString);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);
            if (user == null) return Results.Unauthorized();
            if (!hasher.Verify(user.PasswordHash, currentPassword)) return Results.Unauthorized();
            user.PasswordHash = hasher.Hash(newPassword);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
    }
}