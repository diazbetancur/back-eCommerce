using CC.Domain.Users;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CC.Aplication.Auth
{
    /// <summary>
    /// Servicio unificado de autenticaci�n para el tenant
    /// Soporta tanto usuarios compradores (UserAccount) como staff/admin (TenantUser)
    /// </summary>
    public interface IUnifiedAuthService
    {
        Task<UnifiedAuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<UserProfileDto> GetUserProfileAsync(Guid userId, CancellationToken ct = default);
        Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request, Guid userId, CancellationToken ct = default);
    }

    public class UnifiedAuthService : IUnifiedAuthService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly IConfiguration _configuration;

        public UnifiedAuthService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor,
            IConfiguration configuration)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
            _configuration = configuration;
        }

        /// <summary>
        /// Login unificado: detecta autom�ticamente si es TenantUser (admin/staff) o UserAccount (comprador)
        /// </summary>
        public async Task<UnifiedAuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // 1?? Primero buscar en TenantUser (admin/staff con roles)
            var tenantUser = await db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.ModulePermissions)
                            .ThenInclude(mp => mp.Module)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (tenantUser != null)
            {
                // Es un TenantUser (admin/staff)
                return await LoginTenantUserAsync(tenantUser, request.Password, db, ct);
            }

            // 2?? Si no existe, buscar en UserAccount (comprador)
            var userAccount = await db.UserAccounts
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (userAccount != null)
            {
                // Es un UserAccount (comprador)
                return await LoginUserAccountAsync(userAccount, request.Password, ct);
            }

            // 3?? No existe en ninguna tabla
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        /// <summary>
        /// Login para TenantUser (admin/staff con roles y permisos)
        /// </summary>
        private async Task<UnifiedAuthResponse> LoginTenantUserAsync(
            TenantUser user,
            string password,
            TenantDbContext db,
            CancellationToken ct)
        {
            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is disabled");
            }

            // Verificar password (Identity hasher)
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
            var result = hasher.VerifyHashedPassword(null!, user.PasswordHash, password);

            if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Obtener roles
            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();

            // Obtener permisos por m�dulo
            var permissions = user.UserRoles
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

            // Generar JWT con roles y m�dulos
            var modules = await GetUserModulesAsync(user.Id, db);
            var token = GenerateJwtTokenForTenantUser(user.Id, user.Email, roles, modules);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            return new UnifiedAuthResponse(
                token,
                expiresAt,
                new UnifiedUserDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    UserType = "tenant_user",  // ? Indica que es admin/staff
                    Roles = roles,
                    Permissions = permissions,
                    IsActive = user.IsActive,
                    MustChangePassword = user.MustChangePassword
                }
            );
        }

        /// <summary>
        /// Login para UserAccount (comprador sin roles)
        /// </summary>
        private async Task<UnifiedAuthResponse> LoginUserAccountAsync(
            UserAccount user,
            string password,
            CancellationToken ct)
        {
            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Account is disabled");
            }

            // Verificar password (PBKDF2 custom)
            if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Generar JWT sin roles (comprador)
            var token = GenerateJwtTokenForUserAccount(user.Id, user.Email);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            return new UnifiedAuthResponse(
                token,
                expiresAt,
                new UnifiedUserDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    UserType = "customer",  // ? Indica que es comprador
                    FirstName = user.Profile?.FirstName,
                    LastName = user.Profile?.LastName,
                    Roles = new List<string>(),
                    Permissions = new List<ModulePermissionDto>(),
                    IsActive = user.IsActive
                }
            );
        }

        /// <summary>
        /// Registro de comprador (UserAccount)
        /// </summary>
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Verificar si ya existe (en ambas tablas)
            var existsInTenantUser = await db.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);
            var existsInUserAccount = await db.UserAccounts.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (existsInTenantUser || existsInUserAccount)
            {
                throw new InvalidOperationException("Email already registered");
            }

            // Crear cuenta de comprador (UserAccount)
            var (hash, salt) = HashPassword(request.Password);

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

            var userProfile = new UserProfile
            {
                Id = userAccount.Id,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber
            };

            db.UserProfiles.Add(userProfile);
            await db.SaveChangesAsync(ct);

            var token = GenerateJwtTokenForUserAccount(userAccount.Id, userAccount.Email);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            return new AuthResponse(
                token,
                expiresAt,
                new UserDto(
                    userAccount.Id,
                    userAccount.Email,
                    userProfile.FirstName,
                    userProfile.LastName,
                    userProfile.PhoneNumber,
                    userAccount.CreatedAt,
                    userAccount.IsActive
                )
            );
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

        private async Task<List<string>> GetUserModulesAsync(Guid userId, TenantDbContext db)
        {
            var modules = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.ModulePermissions)
                .Where(rmp => rmp.CanView && rmp.Module.IsActive)
                .Select(rmp => rmp.Module.Code)
                .Distinct()
                .ToListAsync();

            return modules;
        }

        private string GenerateJwtTokenForTenantUser(Guid userId, string email, List<string> roles, List<string> modules)
        {
            var jwtKey = _configuration["jwtKey"] ?? throw new InvalidOperationException("JWT key not configured");

            var payload = new Dictionary<string, object>
            {
                { "sub", userId.ToString() },
                { "email", email },
                { "user_type", "tenant_user" },  // ? Identificador de tipo
                { "jti", Guid.NewGuid().ToString() },
                { "tenant_id", _tenantAccessor.TenantInfo!.Id.ToString() },
                { "tenant_slug", _tenantAccessor.TenantInfo.Slug },
                { "roles", roles },
                { "modules", modules },
                { "exp", new DateTimeOffset(DateTime.UtcNow.AddHours(24)).ToUnixTimeSeconds() },
                { "iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() }
            };

            return CreateJwtToken(payload, jwtKey);
        }

        private string GenerateJwtTokenForUserAccount(Guid userId, string email)
        {
            var jwtKey = _configuration["jwtKey"] ?? throw new InvalidOperationException("JWT key not configured");

            var payload = new Dictionary<string, object>
            {
                { "sub", userId.ToString() },
                { "email", email },
                { "user_type", "customer" },  // ? Identificador de tipo
                { "jti", Guid.NewGuid().ToString() },
                { "tenant_id", _tenantAccessor.TenantInfo!.Id.ToString() },
                { "tenant_slug", _tenantAccessor.TenantInfo.Slug },
                { "exp", new DateTimeOffset(DateTime.UtcNow.AddHours(24)).ToUnixTimeSeconds() },
                { "iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() }
            };

            return CreateJwtToken(payload, jwtKey);
        }

        private string CreateJwtToken(Dictionary<string, object> payload, string key)
        {
            var header = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(new
            {
                alg = "HS256",
                typ = "JWT"
            }));

            var payloadJson = Base64UrlEncode(System.Text.Json.JsonSerializer.Serialize(payload));
            var signature = GenerateSignature($"{header}.{payloadJson}", key);

            return $"{header}.{payloadJson}.{signature}";
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

        private (string hash, string salt) HashPassword(string password)
        {
            byte[] saltBytes = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(256 / 8);

            return (
                Convert.ToBase64String(hashBytes),
                Convert.ToBase64String(saltBytes)
            );
        }

        private bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            byte[] saltBytes = Convert.FromBase64String(storedSalt);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 100000, HashAlgorithmName.SHA256);
            var hashBytes = pbkdf2.GetBytes(256 / 8);

            string hash = Convert.ToBase64String(hashBytes);
            return hash == storedHash;
        }

        /// <summary>
        /// Cambio de contraseña para TenantUser (admin/staff)
        /// Resetea MustChangePassword a false después del cambio exitoso
        /// </summary>
        public async Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request, Guid userId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Buscar el usuario (TenantUser)
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found");
            }

            // Verificar contraseña actual
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
            var result = hasher.VerifyHashedPassword(null!, user.PasswordHash, request.CurrentPassword);

            if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                throw new UnauthorizedAccessException("Current password is incorrect");
            }

            // Validar nueva contraseña
            if (request.NewPassword.Length < 8)
            {
                throw new ArgumentException("New password must be at least 8 characters long");
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                throw new ArgumentException("New password and confirmation do not match");
            }

            // Actualizar contraseña
            user.PasswordHash = hasher.HashPassword(null!, request.NewPassword);
            user.MustChangePassword = false;  // Resetear flag después del cambio exitoso

            await db.SaveChangesAsync(ct);

            return new ChangePasswordResponse(true, "Password changed successfully");
        }
    }

    // ==================== DTOs ====================

    public record UnifiedAuthResponse(
        string Token,
        DateTime ExpiresAt,
        UnifiedUserDto User
    );

    public class UnifiedUserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string UserType { get; set; } = string.Empty;  // "customer" o "tenant_user"
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<ModulePermissionDto> Permissions { get; set; } = new();
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }  // Indica si debe cambiar contraseña en primer login
    }

    public class ModulePermissionDto
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
        public string? IconName { get; set; }
        public bool CanView { get; set; }
        public bool CanCreate { get; set; }
        public bool CanUpdate { get; set; }
        public bool CanDelete { get; set; }
    }

    // ==================== CHANGE PASSWORD DTOs ====================

    public record ChangePasswordRequest(
        string CurrentPassword,
        string NewPassword,
        string ConfirmPassword
    );

    public record ChangePasswordResponse(
        bool Success,
        string Message
    );
}
