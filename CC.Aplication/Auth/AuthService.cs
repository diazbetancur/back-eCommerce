using CC.Domain.Users;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CC.Aplication.Auth
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<UserProfileDto> GetUserProfileAsync(Guid userId, CancellationToken ct = default);
    }

    public class AuthService : IAuthService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly IConfiguration _configuration;

        public AuthService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor,
            IConfiguration configuration)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
            _configuration = configuration;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Verificar si el email ya existe
            var existingUser = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (existingUser != null)
            {
                throw new InvalidOperationException("Email already registered");
            }

            // Buscar rol Customer
            var customerRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Customer", ct);
            if (customerRole == null)
            {
                throw new InvalidOperationException("Customer role not found. Please contact support.");
            }

            // Hash de contraseña usando Identity PasswordHasher
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<CC.Infraestructure.Tenant.Entities.User>();
            var passwordHash = hasher.HashPassword(null!, request.Password);

            // Crear cuenta de usuario
            var user = new CC.Infraestructure.Tenant.Entities.User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.ToLower().Trim(),
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);

            // Asignar rol Customer
            var userRole = new CC.Infraestructure.Tenant.Entities.UserRole
            {
                UserId = user.Id,
                RoleId = customerRole.Id,
                AssignedAt = DateTime.UtcNow
            };

            db.UserRoles.Add(userRole);

            // Crear perfil extendido
            var userProfile = new UserProfile
            {
                Id = user.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber
            };

            db.UserProfiles.Add(userProfile);

            await db.SaveChangesAsync(ct);

            // Generar JWT
            var token = GenerateJwtToken(user, _tenantAccessor.TenantInfo);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            var userDto = new UserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.CreatedAt,
                user.IsActive
            );

            return new AuthResponse(token, expiresAt, userDto);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Buscar usuario por email
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is disabled");
            }

            // Verificar password con Identity.PasswordHasher
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<CC.Infraestructure.Tenant.Entities.User>();
            var result = hasher.VerifyHashedPassword(null!, user.PasswordHash, request.Password);

            if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Generar JWT
            var token = GenerateJwtToken(user, _tenantAccessor.TenantInfo);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            var userDto = new UserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.CreatedAt,
                user.IsActive
            );

            return new AuthResponse(token, expiresAt, userDto);
        }

        public async Task<UserProfileDto> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var profile = await db.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == userId, ct);

            return new UserProfileDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                profile?.DocumentType,
                profile?.DocumentNumber,
                profile?.BirthDate,
                profile?.Address,
                profile?.City,
                profile?.Country,
                user.CreatedAt,
                user.IsActive
            );
        }

        // ==================== PRIVATE HELPERS ====================

        private string GenerateJwtToken(CC.Infraestructure.Tenant.Entities.User user, TenantInfo tenantInfo)
        {
            var jwtKey = _configuration["jwtKey"] ?? throw new InvalidOperationException("JWT key not configured");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("tenant_id", tenantInfo.Id.ToString()),
                new Claim("tenant_slug", tenantInfo.Slug)
            };

            var expires = DateTime.UtcNow.AddHours(24);

            // Crear token manualmente en formato JWT
            var header = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(new
            {
                alg = "HS256",
                typ = "JWT"
            }));

            var payload = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(new
            {
                sub = user.Id.ToString(),
                email = user.Email,
                jti = Guid.NewGuid().ToString(),
                tenant_id = tenantInfo.Id.ToString(),
                tenant_slug = tenantInfo.Slug,
                exp = new DateTimeOffset(expires).ToUnixTimeSeconds(),
                iat = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()
            }));

            var signature = GenerateSignature($"{header}.{payload}", jwtKey);

            return $"{header}.{payload}.{signature}";
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

    // Helper class para PBKDF2
    public static class KeyDerivation
    {
        public static byte[] Pbkdf2(
            string password,
            byte[] salt,
            KeyDerivationPrf prf,
            int iterationCount,
            int numBytesRequested)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(
                password,
                salt,
                iterationCount,
                HashAlgorithmName.SHA256
            );
            return deriveBytes.GetBytes(numBytesRequested);
        }
    }

    public enum KeyDerivationPrf
    {
        HMACSHA1,
        HMACSHA256,
        HMACSHA512
    }
}
