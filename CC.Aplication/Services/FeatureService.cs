using CC.Domain.Tenancy;
using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Cache;
using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CC.Aplication.Services
{
    public interface IFeatureService
    {
        /// <summary>
        /// Verifica si una feature está habilitada para el tenant actual
        /// </summary>
        Task<bool> IsEnabledAsync(string featureKey, CancellationToken ct = default);

        /// <summary>
        /// Obtiene el valor de una feature para el tenant actual
        /// </summary>
        Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, CancellationToken ct = default);

        /// <summary>
        /// Obtiene todos los feature flags del tenant actual
        /// </summary>
        Task<TenantFeatureFlags> GetFeaturesAsync(CancellationToken ct = default);

        /// <summary>
        /// Invalida el cache de features para un tenant específico (para uso de SuperAdmin)
        /// </summary>
        void InvalidateCache(Guid tenantId);
    }

    public class FeatureService : IFeatureService
    {
        private readonly ITenantAccessor _tenantAccessor;
        private readonly AdminDbContext _adminDb;
        private readonly IFeatureCache _cache;
        private readonly ILogger<FeatureService> _logger;

        public FeatureService(
            ITenantAccessor tenantAccessor,
            AdminDbContext adminDb,
            IFeatureCache cache,
            ILogger<FeatureService> logger)
        {
            _tenantAccessor = tenantAccessor;
            _adminDb = adminDb;
            _cache = cache;
            _logger = logger;
        }

        public async Task<bool> IsEnabledAsync(string featureKey, CancellationToken ct = default)
        {
            var features = await GetFeaturesAsync(ct);
            return features.IsEnabled(featureKey);
        }

        public async Task<T?> GetValueAsync<T>(string key, T? defaultValue = default, CancellationToken ct = default)
        {
            var features = await GetFeaturesAsync(ct);
            return features.GetValue(key, defaultValue);
        }

        public async Task<TenantFeatureFlags> GetFeaturesAsync(CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            var tenantId = _tenantAccessor.TenantInfo.Id;

            // Intentar obtener del cache
            var cachedFeatures = _cache.Get(tenantId);
            if (cachedFeatures != null)
            {
                return cachedFeatures;
            }

            // No está en cache, cargar de la base de datos
            _logger.LogDebug("Loading feature flags from database for tenant {TenantId}", tenantId);
            var features = await LoadFeaturesFromDatabaseAsync(tenantId, ct);

            // Guardar en cache
            _cache.Set(tenantId, features);

            return features;
        }

        public void InvalidateCache(Guid tenantId)
        {
            _cache.Invalidate(tenantId);
        }

        private async Task<TenantFeatureFlags> LoadFeaturesFromDatabaseAsync(Guid tenantId, CancellationToken ct)
        {
            var tenant = await _adminDb.Tenants
                .Include(t => t.Plan)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

            if (tenant == null)
            {
                throw new InvalidOperationException($"Tenant {tenantId} not found");
            }

            // Si el tenant tiene feature flags personalizados, usarlos
            if (!string.IsNullOrWhiteSpace(tenant.FeatureFlagsJson))
            {
                try
                {
                    var customFlags = JsonSerializer.Deserialize<TenantFeatureFlags>(tenant.FeatureFlagsJson);
                    if (customFlags != null)
                    {
                        _logger.LogDebug("Using custom feature flags for tenant {TenantId}", tenantId);
                        return customFlags;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error deserializing feature flags for tenant {TenantId}. Using defaults.", tenantId);
                }
            }

            // Si no hay feature flags personalizados, usar los defaults del plan
            var planCode = tenant.Plan?.Code ?? "Basic";
            var defaultFlags = DefaultFeatureFlags.GetForPlan(planCode);
            _logger.LogDebug("Using default feature flags for plan {Plan} for tenant {TenantId}", planCode, tenantId);
            return defaultFlags;
        }
    }
}
