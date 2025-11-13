using CC.Aplication.Features;
using CC.Aplication.Services;
using CC.Domain.Tenancy;
using CC.Infraestructure.AdminDb;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api_eCommerce.Endpoints
{
    public static class FeatureFlagsEndpoints
    {
        public static IEndpointRouteBuilder MapFeatureFlagsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/superadmin/tenants")
                .WithTags("SuperAdmin - Feature Flags");

            group.MapGet("/{tenantId}/features", GetTenantFeatures)
                .WithName("GetTenantFeatures")
                .WithSummary("Obtiene los feature flags de un tenant")
                .Produces<TenantFeaturesResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            group.MapPatch("/{tenantId}/features", UpdateTenantFeatures)
                .WithName("UpdateTenantFeatures")
                .WithSummary("Actualiza los feature flags de un tenant")
                .Produces<TenantFeaturesResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            group.MapDelete("/{tenantId}/features", ResetTenantFeatures)
                .WithName("ResetTenantFeatures")
                .WithSummary("Resetea los feature flags a los defaults del plan")
                .Produces<TenantFeaturesResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            // Endpoints para uso del tenant actual (requieren X-Tenant-Slug)
            var tenantGroup = app.MapGroup("/api/features")
                .WithTags("Feature Flags");

            tenantGroup.MapGet("", GetCurrentTenantFeatures)
                .WithName("GetCurrentTenantFeatures")
                .WithSummary("Obtiene los feature flags del tenant actual")
                .Produces<object>(StatusCodes.Status200OK);

            tenantGroup.MapGet("/{featureKey}", CheckFeature)
                .WithName("CheckFeature")
                .WithSummary("Verifica si una feature específica está habilitada")
                .Produces<FeatureCheckResponse>(StatusCodes.Status200OK);

            return app;
        }

        private static async Task<IResult> GetTenantFeatures(
            Guid tenantId,
            [FromServices] AdminDbContext adminDb,
            [FromServices] ILogger<TenantFeaturesResponse> logger)
        {
            var tenant = await adminDb.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                return Results.NotFound(new { error = "Tenant not found" });
            }

            var usingDefaults = string.IsNullOrWhiteSpace(tenant.FeatureFlagsJson);
            object features;

            if (usingDefaults)
            {
                // Usar defaults del plan
                features = DefaultFeatureFlags.GetForPlan(tenant.Plan ?? "Basic");
            }
            else
            {
                // Deserializar custom features
                try
                {
                    features = JsonSerializer.Deserialize<object>(tenant.FeatureFlagsJson!) ?? new();
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Error deserializing feature flags for tenant {TenantId}", tenantId);
                    features = DefaultFeatureFlags.GetForPlan(tenant.Plan ?? "Basic");
                    usingDefaults = true;
                }
            }

            return Results.Ok(new TenantFeaturesResponse
            {
                TenantId = tenant.Id,
                Slug = tenant.Slug,
                Plan = tenant.Plan ?? "Basic",
                UsingDefaults = usingDefaults,
                Features = features
            });
        }

        private static async Task<IResult> UpdateTenantFeatures(
            Guid tenantId,
            [FromBody] UpdateTenantFeaturesRequest request,
            [FromServices] AdminDbContext adminDb,
            [FromServices] IFeatureService featureService,
            [FromServices] ILogger<TenantFeaturesResponse> logger)
        {
            var tenant = await adminDb.Tenants.FindAsync(tenantId);

            if (tenant == null)
            {
                return Results.NotFound(new { error = "Tenant not found" });
            }

            try
            {
                // Serializar y validar el JSON
                var json = JsonSerializer.Serialize(request.Features, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Intentar deserializar para validar
                JsonSerializer.Deserialize<TenantFeatureFlags>(json);

                // Actualizar
                tenant.FeatureFlagsJson = json;
                tenant.UpdatedAt = DateTime.UtcNow;

                await adminDb.SaveChangesAsync();

                // Invalidar cache
                featureService.InvalidateCache(tenantId);

                logger.LogInformation("Feature flags updated for tenant {TenantId} by SuperAdmin", tenantId);

                return Results.Ok(new TenantFeaturesResponse
                {
                    TenantId = tenant.Id,
                    Slug = tenant.Slug,
                    Plan = tenant.Plan ?? "Basic",
                    UsingDefaults = false,
                    Features = request.Features
                });
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Invalid JSON in feature flags update for tenant {TenantId}", tenantId);
                return Results.Problem(
                    detail: "Invalid feature flags JSON format",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        private static async Task<IResult> ResetTenantFeatures(
            Guid tenantId,
            [FromServices] AdminDbContext adminDb,
            [FromServices] IFeatureService featureService,
            [FromServices] ILogger<TenantFeaturesResponse> logger)
        {
            var tenant = await adminDb.Tenants.FindAsync(tenantId);

            if (tenant == null)
            {
                return Results.NotFound(new { error = "Tenant not found" });
            }

            // Limpiar custom features para usar defaults
            tenant.FeatureFlagsJson = null;
            tenant.UpdatedAt = DateTime.UtcNow;

            await adminDb.SaveChangesAsync();

            // Invalidar cache
            featureService.InvalidateCache(tenantId);

            logger.LogInformation("Feature flags reset to defaults for tenant {TenantId}", tenantId);

            var defaultFeatures = DefaultFeatureFlags.GetForPlan(tenant.Plan ?? "Basic");

            return Results.Ok(new TenantFeaturesResponse
            {
                TenantId = tenant.Id,
                Slug = tenant.Slug,
                Plan = tenant.Plan ?? "Basic",
                UsingDefaults = true,
                Features = defaultFeatures
            });
        }

        private static async Task<IResult> GetCurrentTenantFeatures(
            [FromServices] IFeatureService featureService)
        {
            try
            {
                var features = await featureService.GetFeaturesAsync();
                return Results.Ok(features);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        private static async Task<IResult> CheckFeature(
            string featureKey,
            [FromServices] IFeatureService featureService)
        {
            try
            {
                var isEnabled = await featureService.IsEnabledAsync(featureKey);
                var value = await featureService.GetValueAsync<object>(featureKey);

                return Results.Ok(new FeatureCheckResponse
                {
                    FeatureKey = featureKey,
                    IsEnabled = isEnabled,
                    Value = value
                });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }
    }
}
