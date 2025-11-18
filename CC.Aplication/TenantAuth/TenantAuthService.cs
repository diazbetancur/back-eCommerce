using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CC.Aplication.TenantAuth
{
    public interface ITenantAuthService
    {
        Task<TenantAuthResponse> LoginAsync(TenantLoginRequest request, CancellationToken ct = default);
        Task<TenantUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    }

    public class TenantAuthService : ITenantAuthService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly IConfiguration _configuration;

        public TenantAuthService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor,
            IConfiguration configuration)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
            _configuration = configuration;
        }

        public async Task<TenantAuthResponse> LoginAsync(TenantLoginRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Buscar usuario con roles Y permisos de módulos
            var user = await db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.ModulePermissions)
                            .ThenInclude(mp => mp.Module)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is disabled");
            }

            // Verificar password
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
            var result = hasher.VerifyHashedPassword(null!, user.PasswordHash, request.Password);
            
            if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Obtener roles del usuario
            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

            // ? NUEVO: Obtener permisos agrupados por módulo
            var modulePermissions = user.UserRoles
                .SelectMany(ur => ur.Role.ModulePermissions)
                .Where(mp => mp.Module.IsActive)
                .GroupBy(mp => mp.Module.Code)
                .Select(g => new ModulePermissionDto
                {
                    ModuleCode = g.Key,
                    ModuleName = g.First().Module.Name,
                    IconName = g.First().Module.IconName,
                    CanView = g.Any(p => p.CanView),
                    CanCreate = g.Any(p => p.CanCreate),
                    CanUpdate = g.Any(p => p.CanUpdate),
                    CanDelete = g.Any(p => p.CanDelete)
                })
                .ToList();

            // Generar JWT con roles y módulos
            var token = await GenerateJwtTokenWithRolesAsync(user, roles, _tenantAccessor.TenantInfo, db);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            var userDto = new TenantUserDto(
                user.Id,
                user.Email,
                roles,
                modulePermissions,  // ? NUEVO: Incluir permisos
                user.IsActive,
                user.CreatedAt
            );

            return new TenantAuthResponse(token, expiresAt, userDto);
        }

        public async Task<TenantUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            var user = await db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.ModulePermissions)
                            .ThenInclude(mp => mp.Module)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

            // ? NUEVO: Obtener permisos
            var modulePermissions = user.UserRoles
                .SelectMany(ur => ur.Role.ModulePermissions)
                .Where(mp => mp.Module.IsActive)
                .GroupBy(mp => mp.Module.Code)
                .Select(g => new ModulePermissionDto
                {
                    ModuleCode = g.Key,
                    ModuleName = g.First().Module.Name,
                    IconName = g.First().Module.IconName,
                    CanView = g.Any(p => p.CanView),
                    CanCreate = g.Any(p => p.CanCreate),
                    CanUpdate = g.Any(p => p.CanUpdate),
                    CanDelete = g.Any(p => p.CanDelete)
                })
                .ToList();

            return new TenantUserDto(
                user.Id,
                user.Email,
                roles,
                modulePermissions,  // ? NUEVO
                user.IsActive,
                user.CreatedAt
            );
        }

        // ==================== PRIVATE HELPERS ====================

        private async Task<string> GenerateJwtTokenWithRolesAsync(
            TenantUser user,
            List<string> roles,
            TenantInfo tenantInfo,
            TenantDbContext db)
        {
            var jwtKey = _configuration["jwtKey"] ?? throw new InvalidOperationException("JWT key not configured");
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("tenant_id", tenantInfo.Id.ToString()),
                new Claim("tenant_slug", tenantInfo.Slug)
            };

            // Agregar roles como claims
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            // Agregar módulos disponibles (para el frontend)
            var modules = await GetUserModulesAsync(user.Id, db);
            if (modules.Any())
            {
                claims.Add(new Claim("modules", string.Join(",", modules)));
            }

            var expires = DateTime.UtcNow.AddHours(24);

            // Crear payload
            var payload = new Dictionary<string, object>
            {
                { "sub", user.Id.ToString() },
                { "email", user.Email },
                { "jti", Guid.NewGuid().ToString() },
                { "tenant_id", tenantInfo.Id.ToString() },
                { "tenant_slug", tenantInfo.Slug },
                { "roles", roles },
                { "modules", modules },
                { "exp", new DateTimeOffset(expires).ToUnixTimeSeconds() },
                { "iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() }
            };

            var header = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(new
            {
                alg = "HS256",
                typ = "JWT"
            }));

            var payloadJson = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(payload));
            var signature = GenerateSignature($"{header}.{payloadJson}", jwtKey);

            return $"{header}.{payloadJson}.{signature}";
        }

        private async Task<List<string>> GetUserModulesAsync(Guid userId, TenantDbContext db)
        {
            var modules = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.ModulePermissions)
                .Where(rmp => rmp.CanView)
                .Select(rmp => rmp.Module.Code)
                .Distinct()
                .ToListAsync();

            return modules;
        }

        private string GenerateSignature(string input, string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var hmac = new HMACSHA256(keyBytes);
            var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Base64UrlEncode(Convert.ToBase64String(signatureBytes));
        }

        private string Base64UrlEncode(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            return Base64UrlEncode(Convert.ToBase64String(bytes));
        }

        private string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }

    // ==================== DTOs ====================

    public record TenantLoginRequest(
        string Email,
        string Password
    );

    public record TenantAuthResponse(
        string Token,
        DateTime ExpiresAt,
        TenantUserDto User
    );

    public record TenantUserDto(
        Guid Id,
        string Email,
        List<string> Roles,
        List<ModulePermissionDto> Permissions,  // ? NUEVO
        bool IsActive,
        DateTime CreatedAt
    );

    public record ModulePermissionDto
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string? IconName { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }
    }
}
