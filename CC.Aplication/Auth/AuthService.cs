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
            var existingUser = await db.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (existingUser != null)
            {
                throw new InvalidOperationException("Email already registered");
            }

            // Crear salt y hash de password
            var (hash, salt) = HashPassword(request.Password);

            // Crear cuenta de usuario
            var userAccount = new UserAccount
            {
                Id = Guid.NewGuid(),
                Email = request.Email.ToLower().Trim(),
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.UserAccounts.Add(userAccount);

            // Crear perfil
            var userProfile = new UserProfile
            {
                Id = userAccount.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber
            };

            db.UserProfiles.Add(userProfile);

            await db.SaveChangesAsync(ct);

            // Generar JWT
            var token = GenerateJwtToken(userAccount, _tenantAccessor.TenantInfo);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            var userDto = new UserDto(
                userAccount.Id,
                userAccount.Email,
                userProfile.FirstName,
                userProfile.LastName,
                userProfile.PhoneNumber,
                userAccount.CreatedAt,
                userAccount.IsActive
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
            var userAccount = await db.UserAccounts
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (userAccount == null)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            if (!userAccount.IsActive)
            {
                throw new UnauthorizedAccessException("Account is disabled");
            }

            // Verificar password
            if (!VerifyPassword(request.Password, userAccount.PasswordHash, userAccount.PasswordSalt))
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Generar JWT
            var token = GenerateJwtToken(userAccount, _tenantAccessor.TenantInfo);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            var userDto = new UserDto(
                userAccount.Id,
                userAccount.Email,
                userAccount.Profile?.FirstName ?? "",
                userAccount.Profile?.LastName ?? "",
                userAccount.Profile?.PhoneNumber,
                userAccount.CreatedAt,
                userAccount.IsActive
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

            var userAccount = await db.UserAccounts
                .Include(u => u.Profile)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (userAccount == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var profile = userAccount.Profile;

            return new UserProfileDto(
                userAccount.Id,
                userAccount.Email,
                profile?.FirstName ?? "",
                profile?.LastName ?? "",
                profile?.PhoneNumber,
                profile?.DocumentType,
                profile?.DocumentNumber,
                profile?.BirthDate,
                profile?.Address,
                profile?.City,
                profile?.Country,
                userAccount.CreatedAt,
                userAccount.IsActive
            );
        }

        // ==================== PRIVATE HELPERS ====================

        private (string hash, string salt) HashPassword(string password)
        {
            // Generar salt aleatorio
            byte[] saltBytes = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            // Hash con PBKDF2
            byte[] hashBytes = KeyDerivation.Pbkdf2(
                password: password,
                salt: saltBytes,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8
            );

            return (
                Convert.ToBase64String(hashBytes),
                Convert.ToBase64String(saltBytes)
            );
        }

        private bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            byte[] saltBytes = Convert.FromBase64String(storedSalt);
            byte[] hashBytes = KeyDerivation.Pbkdf2(
                password: password,
                salt: saltBytes,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8
            );

            string hash = Convert.ToBase64String(hashBytes);
            return hash == storedHash;
        }

        private string GenerateJwtToken(UserAccount user, TenantInfo tenantInfo)
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
