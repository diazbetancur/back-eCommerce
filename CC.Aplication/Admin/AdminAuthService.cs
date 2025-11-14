using CC.Infraestructure.Admin;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CC.Aplication.Admin
{
    public interface IAdminAuthService
    {
        Task<AdminLoginResponse> LoginAsync(AdminLoginRequest request, CancellationToken ct = default);
        Task<AdminUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
    }

    public class AdminAuthService : IAdminAuthService
    {
        private readonly AdminDbContext _adminDb;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminAuthService> _logger;

        public AdminAuthService(
            AdminDbContext adminDb,
            IConfiguration configuration,
            ILogger<AdminAuthService> logger)
        {
            _adminDb = adminDb;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AdminLoginResponse> LoginAsync(AdminLoginRequest request, CancellationToken ct = default)
        {
            // Buscar usuario por email
            var user = await _adminDb.AdminUsers
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.AdminRole)
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower(), ct);

            if (user == null || !user.IsActive)
            {
                throw new UnauthorizedAccessException("Invalid credentials or inactive account");
            }

            // Verificar contraseña
            if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                _logger.LogWarning("Failed login attempt for admin user: {Email}", request.Email);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            // Actualizar LastLoginAt
            user.LastLoginAt = DateTime.UtcNow;
            await _adminDb.SaveChangesAsync(ct);

            // Generar JWT
            var roles = user.UserRoles.Select(ur => ur.AdminRole.Name).ToList();
            var token = GenerateJwtToken(user.Id, user.Email, roles);

            _logger.LogInformation("Admin user logged in: {Email}", user.Email);

            return new AdminLoginResponse(
                token,
                DateTime.UtcNow.AddHours(24),
                new AdminUserDto(
                    user.Id,
                    user.Email,
                    user.FullName,
                    user.IsActive,
                    roles,
                    user.CreatedAt,
                    user.LastLoginAt
                )
            );
        }

        public async Task<AdminUserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _adminDb.AdminUsers
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.AdminRole)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                throw new InvalidOperationException("Admin user not found");
            }

            var roles = user.UserRoles.Select(ur => ur.AdminRole.Name).ToList();

            return new AdminUserDto(
                user.Id,
                user.Email,
                user.FullName,
                user.IsActive,
                roles,
                user.CreatedAt,
                user.LastLoginAt
            );
        }

        // ==================== PRIVATE HELPERS ====================

        private string GenerateJwtToken(Guid userId, string email, List<string> roles)
        {
            var jwtKey = _configuration["jwtKey"] ?? throw new InvalidOperationException("JWT key not configured");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim("admin", "true"), // Marca especial para identificar admin
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            // Agregar roles como claims
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = credentials
            };

            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static bool VerifyPassword(string password, string passwordHash, string passwordSalt)
        {
            var saltBytes = Convert.FromBase64String(passwordSalt);
            var hashBytes = Convert.FromBase64String(passwordHash);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
            var testHash = pbkdf2.GetBytes(32);

            return testHash.SequenceEqual(hashBytes);
        }

        public static (string Hash, string Salt) HashPassword(string password)
        {
            var saltBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(32);

            return (
                Convert.ToBase64String(hashBytes),
                Convert.ToBase64String(saltBytes)
            );
        }
    }
}
