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
                .WithSummary("Get current user profile")
                .WithDescription("Returns the authenticated user's profile information")
                .Produces<UserProfileDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            return app;
        }

        private static async Task<IResult> Register(
            HttpContext context,
            [FromBody] RegisterRequest request,
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

                // Validaciones básicas
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
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Registration Failed",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred during registration"
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

                // Validaciones básicas
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "Email and password are required"
                    );
                }

                // Autenticar usuario (detecta automáticamente si es admin o comprador)
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
            catch (Exception ex)
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
            CC.Aplication.Auth.IUnifiedAuthService unifiedAuthService,
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
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving profile"
                );
            }
        }
    }
}