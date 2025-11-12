using Api_eCommerce.Auth;
using Api_eCommerce.Workers;
using CC.Aplication.Provisioning;
using CC.Domain.Tenancy;
using CC.Infraestructure.AdminDb;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace Api_eCommerce.Endpoints
{
    public static class ProvisioningEndpoints
    {
        private static readonly string[] AllowedPlans = { "Basic", "Premium", "Enterprise" };

        public static IEndpointRouteBuilder MapProvisioningEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/provision/tenants")
                .WithTags("Tenant Provisioning");

            group.MapPost("/init", InitProvisioning)
                .WithName("InitProvisioning")
                .WithSummary("Inicializa el aprovisionamiento de un nuevo tenant")
                .WithDescription("Crea un tenant en estado PENDING_VALIDATION y genera un token de confirmación válido por 15 minutos")
                .Produces<InitProvisioningResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

            group.MapPost("/confirm", ConfirmProvisioning)
                .WithName("ConfirmProvisioning")
                .WithSummary("Confirma el aprovisionamiento y lo encola para procesamiento")
                .WithDescription("Valida el token de confirmación y encola el tenant para aprovisionamiento en background")
                .Produces<ConfirmProvisioningResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            group.MapGet("/{provisioningId}/status", GetProvisioningStatus)
                .WithName("GetProvisioningStatus")
                .WithSummary("Obtiene el estado del aprovisionamiento")
                .WithDescription("Retorna el estado actual y el historial de pasos del aprovisionamiento")
                .Produces<ProvisioningStatusResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            return app;
        }

        private static async Task<IResult> InitProvisioning(
            [FromBody] InitProvisioningRequest request,
            AdminDbContext adminDb,
            IConfirmTokenService tokenService,
            ILogger<InitProvisioningRequest> logger)
        {
            try
            {
                // Validar plan
                if (!AllowedPlans.Contains(request.Plan, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.Problem(
                        title: "Invalid Plan",
                        detail: $"Plan '{request.Plan}' is not allowed. Allowed plans: {string.Join(", ", AllowedPlans)}",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Verificar si el slug ya existe
                var existingTenant = await adminDb.Tenants
                    .FirstOrDefaultAsync(t => t.Slug == request.Slug.ToLower());

                if (existingTenant != null)
                {
                    return Results.Problem(
                        title: "Slug Already Exists",
                        detail: $"A tenant with slug '{request.Slug}' already exists",
                        statusCode: StatusCodes.Status409Conflict);
                }

                // Crear tenant en estado PENDING_VALIDATION
                var dbName = $"ecom_tenant_{request.Slug.ToLower()}";
                var tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Slug = request.Slug.ToLower(),
                    Plan = request.Plan,
                    DbName = dbName,
                    Status = "PENDING_VALIDATION",
                    CreatedAt = DateTime.UtcNow
                };

                adminDb.Tenants.Add(tenant);
                await adminDb.SaveChangesAsync();

                // Crear registro inicial de aprovisionamiento
                var provisioning = new TenantProvisioning
                {
                    TenantId = tenant.Id,
                    Step = "Init",
                    Status = "Pending",
                    StartedAt = DateTime.UtcNow,
                    Message = "Provisioning initialized, waiting for confirmation"
                };

                adminDb.TenantProvisionings.Add(provisioning);
                await adminDb.SaveChangesAsync();

                // Generar token de confirmación (válido por 15 minutos)
                var confirmToken = tokenService.GenerateConfirmToken(tenant.Id, tenant.Slug);

                logger.LogInformation(
                    "Tenant provisioning initialized. TenantId: {TenantId}, Slug: {Slug}, Plan: {Plan}",
                    tenant.Id, tenant.Slug, tenant.Plan);

                // TODO: Implementar rate limiting
                // TODO: Implementar captcha/hmac validation

                var response = new InitProvisioningResponse
                {
                    ProvisioningId = tenant.Id,
                    ConfirmToken = confirmToken,
                    Next = "/provision/tenants/confirm",
                    Message = "Provisioning initialized. Use the confirmation token within 15 minutes to proceed."
                };

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error initializing tenant provisioning");
                return Results.Problem(
                    title: "Internal Server Error",
                    detail: "An error occurred while initializing provisioning",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        private static async Task<IResult> ConfirmProvisioning(
            HttpContext context,
            AdminDbContext adminDb,
            IConfirmTokenService tokenService,
            TenantProvisioningWorker worker,
            ILogger<ConfirmProvisioningResponse> logger)
        {
            try
            {
                // Extraer y validar token de autorización
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Results.Problem(
                        title: "Missing Authorization",
                        detail: "Authorization header with Bearer token is required",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var token = authHeader.Substring("Bearer ".Length).Trim();
                var principal = tokenService.ValidateConfirmToken(token);

                if (principal == null)
                {
                    return Results.Problem(
                        title: "Invalid Token",
                        detail: "The confirmation token is invalid or has expired",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                // Extraer provisioningId del token
                var provisioningIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
                if (provisioningIdClaim == null || !Guid.TryParse(provisioningIdClaim.Value, out var provisioningId))
                {
                    return Results.Problem(
                        title: "Invalid Token Claims",
                        detail: "Token does not contain a valid provisioning ID",
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                // Buscar tenant
                var tenant = await adminDb.Tenants.FindAsync(provisioningId);
                if (tenant == null)
                {
                    return Results.Problem(
                        title: "Tenant Not Found",
                        detail: $"Tenant with ID {provisioningId} not found",
                        statusCode: StatusCodes.Status404NotFound);
                }

                // Verificar estado
                if (tenant.Status != "PENDING_VALIDATION")
                {
                    return Results.Problem(
                        title: "Invalid State",
                        detail: $"Tenant is in '{tenant.Status}' state and cannot be confirmed",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Cambiar estado a QUEUED
                tenant.Status = "QUEUED";
                tenant.UpdatedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();

                // Encolar para procesamiento en background
                await worker.EnqueueProvisioningAsync(tenant.Id);

                logger.LogInformation(
                    "Tenant provisioning confirmed and queued. TenantId: {TenantId}, Slug: {Slug}",
                    tenant.Id, tenant.Slug);

                var response = new ConfirmProvisioningResponse
                {
                    ProvisioningId = tenant.Id,
                    Status = "QUEUED",
                    Message = "Provisioning confirmed and queued for processing",
                    StatusEndpoint = $"/provision/tenants/{tenant.Id}/status"
                };

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error confirming tenant provisioning");
                return Results.Problem(
                    title: "Internal Server Error",
                    detail: "An error occurred while confirming provisioning",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        private static async Task<IResult> GetProvisioningStatus(
            Guid provisioningId,
            AdminDbContext adminDb,
            ILogger<ProvisioningStatusResponse> logger)
        {
            try
            {
                var tenant = await adminDb.Tenants
                    .Include(t => t.ProvisioningHistory)
                    .FirstOrDefaultAsync(t => t.Id == provisioningId);

                if (tenant == null)
                {
                    return Results.Problem(
                        title: "Tenant Not Found",
                        detail: $"Tenant with ID {provisioningId} not found",
                        statusCode: StatusCodes.Status404NotFound);
                }

                var steps = tenant.ProvisioningHistory
                    .OrderBy(p => p.StartedAt)
                    .Select(p => new ProvisioningStepDto
                    {
                        Step = p.Step,
                        Status = p.Status,
                        StartedAt = p.StartedAt,
                        CompletedAt = p.CompletedAt,
                        Log = p.Message,
                        ErrorMessage = p.ErrorMessage
                    })
                    .ToList();

                var response = new ProvisioningStatusResponse
                {
                    Status = tenant.Status,
                    TenantSlug = tenant.Status == "Active" ? tenant.Slug : null,
                    DbName = tenant.Status == "Active" ? tenant.DbName : null,
                    Steps = steps
                };

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting provisioning status for {ProvisioningId}", provisioningId);
                return Results.Problem(
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving provisioning status",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
