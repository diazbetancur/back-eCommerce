using CC.Aplication.Auth;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Api_eCommerce.Auth
{
    public static class TenantAuthEndpoints
    {
        public static IEndpointRouteBuilder MapTenantAuth(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/auth")
                .WithTags("Authentication");

            var publicGroup = app.MapGroup("/api/auth")
                .WithTags("Authentication");

            group.MapPost("/register", Register)
                .WithName("Register")
                .WithSummary("Register a new user account")
                .WithDescription("Creates a new user account for the tenant")
                .Produces<AuthResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

            group.MapPost("/login", Login)
                .WithName("Login")
                .WithSummary("Unified authentication - Detects admin or customer automatically")
                .WithDescription("Authenticates user (TenantUser or UserAccount) and returns JWT token with appropriate permissions")
                .Produces<UnifiedAuthResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            group.MapGet("/me", GetProfile)
                .RequireAuthorization()
                .WithName("GetProfile")
                .WithSummary("Get current user profile (tenant user)")
                .WithDescription("Returns the authenticated tenant user's profile information. REQUIRES X-Tenant-Slug header. For admin users, use GET /admin/auth/me instead.")
                .Produces<UserProfileDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            group.MapPut("/profile", UpdateProfile)
                .RequireAuthorization()
                .WithName("UpdateProfile")
                .WithSummary("Update current user profile")
                .WithDescription("Updates the authenticated user's profile information (name, phone, address, etc.). Email cannot be changed.")
                .Produces<UserProfileDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            group.MapPost("/change-password", ChangePassword)
                .RequireAuthorization()
                .WithName("ChangePassword")
                .WithSummary("Change user password")
                .WithDescription("Changes the current user's password. Required when MustChangePassword is true after first login.")
                .Produces<ChangePasswordResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            publicGroup.MapPost("/activate-account", ActivateAccount)
                .AllowAnonymous()
                .WithName("ActivateAccount")
                .WithSummary("Activate the tenant admin account")
                .WithDescription("Activates a pending tenant admin account using a one-time token and defines the initial password.")
                .Produces<AuthOperationResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

            publicGroup.MapPost("/forgot-password", ForgotPassword)
                .AllowAnonymous()
                .WithName("ForgotPassword")
                .WithSummary("Request password assistance or resend activation")
                .WithDescription("Generates a password reset token for active accounts or resends activation for pending tenant admins.")
                .Produces<AuthOperationResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

            publicGroup.MapPost("/reset-password", ResetPassword)
                .AllowAnonymous()
                .WithName("ResetPassword")
                .WithSummary("Reset password using a one-time token")
                .WithDescription("Consumes a one-time password reset token and updates the password for an active user.")
                .Produces<AuthOperationResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

            return app;
        }

        private static async Task<IResult> Register(
            HttpContext context,
            [FromBody] RegisterRequest request,
            CC.Aplication.Auth.IUnifiedAuthService unifiedAuthService,  // ? CAMBIO
            ITenantResolver tenantResolver,
            ILogger<RegisterRequest> logger)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Validaciones b�sicas
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "Email is required"
                    );
                }

                if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "Password must be at least 8 characters long"
                    );
                }

                if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "First name and last name are required"
                    );
                }

                // Registrar usuario
                var response = await unifiedAuthService.RegisterAsync(request);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Email already registered"))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Email Already Exists",
                    detail: ex.Message
                );
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Registration failed for email {Email}: {Message}", request.Email, ex.Message);
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Registration Failed",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during registration for email {Email}", request.Email);
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: $"An error occurred during registration: {ex.Message}"
                );
            }
        }

        private static async Task<IResult> Login(
            HttpContext context,
            [FromBody] LoginRequest request,
            CC.Aplication.Auth.IUnifiedAuthService unifiedAuthService,  // ? CAMBIO
            ITenantResolver tenantResolver)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Validaciones b�sicas
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "Email and password are required"
                    );
                }

                // Autenticar usuario (detecta autom�ticamente si es admin o comprador)
                var response = await unifiedAuthService.LoginAsync(request);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Authentication Failed",
                    detail: ex.Message
                );
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Login Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred during login"
                );
            }
        }

        private static async Task<IResult> GetProfile(
            HttpContext context,
            CC.Aplication.Auth.IUnifiedAuthService unifiedAuthService)
        {
            try
            {
                // El tenant ya fue resuelto por el filtro RequireTenantResolution()
                // No necesitamos validarlo de nuevo aquí

                // DEBUG: Ver todos los claims disponibles
                var allClaims = string.Join(", ", context.User.Claims.Select(c => $"{c.Type}={c.Value}"));
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("🔍 Claims in token: {Claims}", allClaims);
                logger.LogInformation("🔍 User.Identity.IsAuthenticated: {IsAuth}", context.User.Identity?.IsAuthenticated);

                // Obtener user ID del token JWT
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirst("sub")
                    ?? context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

                if (userIdClaim == null)
                {
                    logger.LogWarning("❌ No user ID claim found. Available claims: {Claims}", allClaims);
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: $"User ID not found in token. Available claims: {allClaims}"
                    );
                }

                if (!Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    logger.LogWarning("❌ Cannot parse user ID: {Value}", userIdClaim.Value);
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: $"Invalid user ID format: {userIdClaim.Value}"
                    );
                }

                // Obtener perfil
                var profile = await unifiedAuthService.GetUserProfileAsync(userId);
                return Results.Ok(profile);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("User not found"))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "User Not Found",
                    detail: ex.Message
                );
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Profile Retrieval Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving profile"
                );
            }
        }

        private static async Task<IResult> UpdateProfile(
            HttpContext context,
            [FromBody] UpdateProfileRequest request,
            CC.Aplication.Auth.IUnifiedAuthService unifiedAuthService)
        {
            try
            {
                // Obtener user ID del token JWT
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirst("sub")
                    ?? context.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Validaciones básicas
                if (string.IsNullOrWhiteSpace(request.FirstName))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "First name is required"
                    );
                }

                if (string.IsNullOrWhiteSpace(request.LastName))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "Last name is required"
                    );
                }

                // Actualizar perfil
                var updatedProfile = await unifiedAuthService.UpdateUserProfileAsync(userId, request);
                return Results.Ok(updatedProfile);
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Error",
                    detail: ex.Message
                );
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("User not found"))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "User Not Found",
                    detail: ex.Message
                );
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Profile Update Failed",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Unexpected error during profile update for user {UserId}",
                    context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while updating profile"
                );
            }
        }

        private static async Task<IResult> ChangePassword(
            HttpContext context,
            [FromBody] ChangePasswordRequest request,
            CC.Aplication.Auth.IUnifiedAuthService unifiedAuthService)
        {
            try
            {
                // El tenant ya fue resuelto por el filtro RequireTenantResolution()
                // No necesitamos validarlo de nuevo aquí

                // Obtener user ID del token JWT
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirst("sub");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Validaciones básicas
                if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "Current password is required"
                    );
                }

                if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "New password must be at least 8 characters long"
                    );
                }

                // Cambiar contraseña
                var response = await unifiedAuthService.ChangePasswordAsync(request, userId);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Password Change Failed",
                    detail: ex.Message
                );
            }
            catch (ArgumentException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Validation Error",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while changing password"
                );
            }
        }

        private static async Task<IResult> ActivateAccount(
            HttpContext context,
            [FromBody] ActivateAccountRequest request,
            ITenantAccountSecurityService tenantAccountSecurityService)
        {
            var result = await tenantAccountSecurityService.ActivateAccountAsync(
                request.Token,
                request.Password,
                request.ConfirmPassword,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString());

            if (!result.Success)
            {
                return ToAuthProblem(result, "Account Activation Failed");
            }

            return Results.Ok(new AuthOperationResponse(true, result.Message));
        }

        private static async Task<IResult> ForgotPassword(
            [FromBody] ForgotPasswordRequest request,
            ITenantAccountSecurityService tenantAccountSecurityService)
        {
            var result = await tenantAccountSecurityService.RequestPasswordAssistanceAsync(request.Email);
            return Results.Ok(new AuthOperationResponse(true, result.Message));
        }

        private static async Task<IResult> ResetPassword(
            HttpContext context,
            [FromBody] ResetPasswordRequest request,
            ITenantAccountSecurityService tenantAccountSecurityService)
        {
            var result = await tenantAccountSecurityService.ResetPasswordAsync(
                request.Token,
                request.Password,
                request.ConfirmPassword,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString());

            if (!result.Success)
            {
                return ToAuthProblem(result, "Password Reset Failed");
            }

            return Results.Ok(new AuthOperationResponse(true, result.Message));
        }

        private static IResult ToAuthProblem(AccountSecurityOperationResult result, string title)
        {
            var statusCode = result.ErrorCode switch
            {
                "PASSWORD_CONFIRMATION_MISMATCH" => StatusCodes.Status400BadRequest,
                "PASSWORD_POLICY_NOT_MET" => StatusCodes.Status400BadRequest,
                "INVALID_ACTIVATION_TOKEN" => StatusCodes.Status400BadRequest,
                "EXPIRED_ACTIVATION_TOKEN" => StatusCodes.Status400BadRequest,
                "INVALID_PASSWORD_RESET_TOKEN" => StatusCodes.Status400BadRequest,
                "EXPIRED_PASSWORD_RESET_TOKEN" => StatusCodes.Status400BadRequest,
                "TENANT_REQUIRED" => StatusCodes.Status400BadRequest,
                "TENANT_MISMATCH" => StatusCodes.Status409Conflict,
                "USED_ACTIVATION_TOKEN" => StatusCodes.Status409Conflict,
                "REVOKED_ACTIVATION_TOKEN" => StatusCodes.Status409Conflict,
                "TENANT_NOT_PENDING_ACTIVATION" => StatusCodes.Status409Conflict,
                "USER_NOT_PENDING_ACTIVATION" => StatusCodes.Status409Conflict,
                "USED_PASSWORD_RESET_TOKEN" => StatusCodes.Status409Conflict,
                "REVOKED_PASSWORD_RESET_TOKEN" => StatusCodes.Status409Conflict,
                "USER_NOT_ACTIVE" => StatusCodes.Status409Conflict,
                "TENANT_SYNC_FAILED" => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status400BadRequest
            };

            return Results.Problem(
                statusCode: statusCode,
                title: title,
                detail: result.Message,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = result.ErrorCode
                });
        }
    }

    public record ActivateAccountRequest(string Token, string Password, string ConfirmPassword);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Token, string Password, string ConfirmPassword);
    public record AuthOperationResponse(bool Success, string Message);
}