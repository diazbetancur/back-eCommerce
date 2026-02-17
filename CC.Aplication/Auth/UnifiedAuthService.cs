using CC.Domain.Users;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CC.Aplication.Auth
{
    /// <summary>
    /// Servicio de autenticación unificado
    /// Todos los usuarios (admins, staff, clientes) usan la misma tabla Users con roles diferentes
    /// </summary>
    public interface IUnifiedAuthService
    {
        Task<UnifiedAuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task<UserProfileDto> GetUserProfileAsync(Guid userId, CancellationToken ct = default);
        Task<UserProfileDto> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
        Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request, Guid userId, CancellationToken ct = default);
    }

    public class UnifiedAuthService : IUnifiedAuthService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UnifiedAuthService> _logger;
        private readonly CC.Aplication.Services.IFeatureService _featureService;

        public UnifiedAuthService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor,
            IConfiguration configuration,
            ILogger<UnifiedAuthService> logger,
            CC.Aplication.Services.IFeatureService featureService)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
            _configuration = configuration;
            _logger = logger;
            _featureService = featureService;
        }

        /// <summary>
        /// Login unificado: todos los usuarios usan la misma tabla Users
        /// La diferenciación es por roles (SuperAdmin, Customer, etc.)
        /// </summary>
        public async Task<UnifiedAuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                _logger.LogError("🔴 Login failed: No tenant context available");
                throw new InvalidOperationException("No tenant context available");
            }

            _logger.LogInformation("🔍 Login attempt for {Email} in tenant {Slug} (DB: {DbName})",
                request.Email, _tenantAccessor.TenantInfo.Slug, _tenantAccessor.TenantInfo.DbName);

            await using var db = _dbFactory.Create();

            // Buscar usuario en tabla Users
            var user = await db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.ModulePermissions)
                            .ThenInclude(mp => mp.Module)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (user == null)
            {
                _logger.LogError("🔴 User {Email} not found", request.Email);
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            _logger.LogInformation("✅ Found user: {Email}, IsActive: {IsActive}, MustChangePassword: {MustChange}",
                user.Email, user.IsActive, user.MustChangePassword);

            if (!user.IsActive)
            {
                _logger.LogWarning("🔴 Login failed: User {Email} is not active", user.Email);
                throw new UnauthorizedAccessException("Account is disabled");
            }

            // Verificar password (Identity hasher)
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(null!, user.PasswordHash, request.Password);

            _logger.LogInformation("🔑 Password verification result for {Email}: {Result}", user.Email, result);

            if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            {
                _logger.LogError("🔴 Login failed: Invalid password for {Email}", user.Email);
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Obtener roles
            _logger.LogInformation("📋 Getting roles for user {UserId}...", user.Id);
            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            _logger.LogInformation("📋 Found {RoleCount} roles: {Roles}", roles.Count, string.Join(", ", roles));

            // Obtener permisos por módulo
            _logger.LogInformation("🔐 Getting permissions for user {UserId}...", user.Id);
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
            _logger.LogInformation("🔐 Found {PermissionCount} module permissions", permissions.Count);

            // Generar JWT con roles y módulos
            _logger.LogInformation("🎫 Getting modules for JWT...");
            var modules = await GetUserModulesAsync(user.Id, db);
            _logger.LogInformation("🎫 Found {ModuleCount} modules: {Modules}", modules.Count, string.Join(", ", modules));

            _logger.LogInformation("🔑 Generating JWT token...");
            var token = GenerateJwtToken(user.Id, user.Email, roles, modules);
            var expiresAt = DateTime.UtcNow.AddHours(24);

            // Cargar features del plan para incluirlas en la respuesta de login
            _logger.LogInformation("🎯 Loading tenant features...");
            var tenantFeatures = await _featureService.GetFeaturesAsync(ct);
            var featuresDto = new TenantFeaturesDto
            {
                AllowGuestCheckout = tenantFeatures.AllowGuestCheckout,
                EnableExpressCheckout = tenantFeatures.EnableExpressCheckout,
                ShowStock = tenantFeatures.ShowStock,
                HasVariants = tenantFeatures.HasVariants,
                EnableMultiStore = tenantFeatures.EnableMultiStore,
                EnableWishlist = tenantFeatures.EnableWishlist,
                EnableReviews = tenantFeatures.EnableReviews,
                EnableCartSave = tenantFeatures.EnableCartSave,
                MaxCartItems = tenantFeatures.MaxCartItems,
                EnableAdvancedSearch = tenantFeatures.EnableAdvancedSearch,
                EnableAnalytics = tenantFeatures.EnableAnalytics,
                EnableNewsletterSignup = tenantFeatures.EnableNewsletterSignup,
                Payments = new PaymentFeaturesDto
                {
                    WompiEnabled = tenantFeatures.Payments.WompiEnabled,
                    StripeEnabled = tenantFeatures.Payments.StripeEnabled,
                    PayPalEnabled = tenantFeatures.Payments.PayPalEnabled,
                    CashOnDelivery = tenantFeatures.Payments.CashOnDelivery
                }
            };

            _logger.LogInformation("✅ Login successful for user {Email} with {RoleCount} roles", user.Email, roles.Count);

            return new UnifiedAuthResponse(
                token,
                expiresAt,
                new UnifiedUserDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PhoneNumber = user.PhoneNumber,
                    Roles = roles,
                    Modules = modules,  // Array simple de códigos: ["catalog", "orders", ...]
                    Permissions = permissions,
                    IsActive = user.IsActive,
                    MustChangePassword = user.MustChangePassword,
                    Features = featuresDto  // Features basadas en el plan del tenant
                }
            );
        }

        /// <summary>
        /// Registro de nuevo usuario (cliente)
        /// Automáticamente se le asigna el rol "Customer"
        /// </summary>
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Verificar si ya existe
            var exists = await db.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (exists)
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
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            var passwordHash = hasher.HashPassword(null!, request.Password);

            // Crear usuario
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email.ToLower().Trim(),
                PasswordHash = passwordHash,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                MustChangePassword = false,
                TenantId = _tenantAccessor.TenantInfo.Id, // ✅ Asignar tenant
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);

            // Asignar rol Customer
            var userRole = new UserRole
            {
                UserId = user.Id,
                RoleId = customerRole.Id,
                AssignedAt = DateTime.UtcNow
            };

            db.UserRoles.Add(userRole);

            // ✅ Save user and role
            await db.SaveChangesAsync(ct);

            // Note: UserProfile is optional and can be created later via profile update endpoint

            // Generar JWT
            var token = GenerateJwtToken(user.Id, user.Email, new List<string> { "Customer" }, new List<string>());
            var expiresAt = DateTime.UtcNow.AddHours(24);

            return new AuthResponse(
                token,
                expiresAt,
                new UserDto(
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.PhoneNumber,
                    user.CreatedAt,
                    user.IsActive
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

        public async Task<UserProfileDto> UpdateUserProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            // Validations
            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                throw new ArgumentException("First name is required", nameof(request.FirstName));
            }

            if (string.IsNullOrWhiteSpace(request.LastName))
            {
                throw new ArgumentException("Last name is required", nameof(request.LastName));
            }

            await using var db = _dbFactory.Create();

            // Get user
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            // Update user basic info
            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.PhoneNumber = request.PhoneNumber?.Trim();

            await db.SaveChangesAsync(ct);

            // Update or create UserProfile (optional extended data)
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.Id == userId, ct);

            if (profile == null && HasExtendedProfileData(request))
            {
                // Create profile if it doesn't exist and user provides extended data
                profile = new UserProfile
                {
                    Id = userId,
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    PhoneNumber = request.PhoneNumber?.Trim(),
                    DocumentType = request.DocumentType?.Trim(),
                    DocumentNumber = request.DocumentNumber?.Trim(),
                    BirthDate = request.BirthDate,
                    Address = request.Address?.Trim(),
                    City = request.City?.Trim(),
                    Country = request.Country?.Trim()
                };
                db.UserProfiles.Add(profile);
            }
            else if (profile != null)
            {
                // Update existing profile
                profile.FirstName = request.FirstName.Trim();
                profile.LastName = request.LastName.Trim();
                profile.PhoneNumber = request.PhoneNumber?.Trim();
                profile.DocumentType = request.DocumentType?.Trim();
                profile.DocumentNumber = request.DocumentNumber?.Trim();
                profile.BirthDate = request.BirthDate;
                profile.Address = request.Address?.Trim();
                profile.City = request.City?.Trim();
                profile.Country = request.Country?.Trim();
            }

            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Profile updated for user {UserId}", userId);

            // Return updated profile
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

        private bool HasExtendedProfileData(UpdateProfileRequest request)
        {
            return !string.IsNullOrWhiteSpace(request.DocumentType) ||
                   !string.IsNullOrWhiteSpace(request.DocumentNumber) ||
                   request.BirthDate.HasValue ||
                   !string.IsNullOrWhiteSpace(request.Address) ||
                   !string.IsNullOrWhiteSpace(request.City) ||
                   !string.IsNullOrWhiteSpace(request.Country);
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

        private string GenerateJwtToken(Guid userId, string email, List<string> roles, List<string> modules)
        {
            try
            {
                _logger.LogInformation("🔑 Starting JWT generation for {Email}...", email);

                var jwtKey = _configuration["jwtKey"];
                if (string.IsNullOrEmpty(jwtKey))
                {
                    _logger.LogError("🔴 JWT key is not configured!");
                    throw new InvalidOperationException("JWT key not configured");
                }

                _logger.LogInformation("🔑 JWT key found, creating payload...");

                // Obtener issuer y audience de configuración
                var issuer = _configuration["Jwt:Issuer"] ?? "ecommerce-api";
                var audience = _configuration["Jwt:Audience"] ?? "ecommerce-clients";

                var payload = new Dictionary<string, object>
                {
                    { "sub", userId.ToString() },
                    { "email", email },
                    { "jti", Guid.NewGuid().ToString() },
                    { "iss", issuer },
                    { "aud", audience },
                    { "tenant_id", _tenantAccessor.TenantInfo!.Id.ToString() },
                    { "tenant_slug", _tenantAccessor.TenantInfo.Slug },
                    { "roles", roles },
                    { "modules", modules },
                    { "exp", new DateTimeOffset(DateTime.UtcNow.AddHours(24)).ToUnixTimeSeconds() },
                    { "iat", new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() }
                };

                _logger.LogInformation("🔑 Payload created, generating token...");
                var token = CreateJwtToken(payload, jwtKey);
                _logger.LogInformation("✅ JWT token generated successfully");

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔴 Error generating JWT token for {Email}", email);
                throw;
            }
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
            return Base64UrlEncode(signatureBytes);
        }

        private string Base64UrlEncode(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            return Base64UrlEncode(bytes);
        }

        private string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Cambio de contraseña
        /// Resetea MustChangePassword a false después del cambio exitoso
        /// </summary>
        public async Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request, Guid userId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Buscar el usuario
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found");
            }

            // Verificar contraseña actual
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
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
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string> Roles { get; set; } = new();
        public List<string> Modules { get; set; } = new();  // Array simple de códigos de módulos para frontend
        public List<ModulePermissionDto> Permissions { get; set; } = new();
        public bool IsActive { get; set; }
        public bool MustChangePassword { get; set; }  // Indica si debe cambiar contraseña

        // Features del plan - permite al frontend armar menú basado en plan + permisos
        public TenantFeaturesDto? Features { get; set; }
    }

    /// <summary>
    /// Features del tenant basadas en su plan - incluido en login para simplificar frontend
    /// </summary>
    public class TenantFeaturesDto
    {
        // Checkout Features
        public bool AllowGuestCheckout { get; set; }
        public bool EnableExpressCheckout { get; set; }

        // Catalog Features
        public bool ShowStock { get; set; }
        public bool HasVariants { get; set; }
        public bool EnableMultiStore { get; set; }  // Determina si mostrar menú de Stores
        public bool EnableWishlist { get; set; }
        public bool EnableReviews { get; set; }

        // Cart & Search
        public bool EnableCartSave { get; set; }
        public int MaxCartItems { get; set; }
        public bool EnableAdvancedSearch { get; set; }

        // Marketing
        public bool EnableAnalytics { get; set; }
        public bool EnableNewsletterSignup { get; set; }

        // Payment Methods
        public PaymentFeaturesDto Payments { get; set; } = new();
    }

    public class PaymentFeaturesDto
    {
        public bool WompiEnabled { get; set; }
        public bool StripeEnabled { get; set; }
        public bool PayPalEnabled { get; set; }
        public bool CashOnDelivery { get; set; }
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
        string NewPassword
    );

    public record ChangePasswordResponse(
        bool Success,
        string Message
    );
}
