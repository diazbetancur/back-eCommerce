using CC.Domain.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CC.Infraestructure.Cache
{
    /// <summary>
    /// Cache especializado para feature flags de tenants
    /// </summary>
    public interface IFeatureCache
    {
        /// <summary>
        /// Obtiene los feature flags de un tenant desde el cache
        /// </summary>
        TenantFeatureFlags? Get(Guid tenantId);

        /// <summary>
        /// Guarda los feature flags de un tenant en el cache
        /// </summary>
        void Set(Guid tenantId, TenantFeatureFlags features);

        /// <summary>
        /// Invalida el cache de un tenant específico
        /// </summary>
        void Invalidate(Guid tenantId);

        /// <summary>
        /// Limpia todo el cache de features
        /// </summary>
        void Clear();
    }

    public class FeatureCache : IFeatureCache
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<FeatureCache> _logger;

        private static readonly TimeSpan AbsoluteExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(5);

        public FeatureCache(IMemoryCache cache, ILogger<FeatureCache> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public TenantFeatureFlags? Get(Guid tenantId)
        {
            var cacheKey = GetCacheKey(tenantId);
            
            if (_cache.TryGetValue<TenantFeatureFlags>(cacheKey, out var features))
            {
                _logger.LogDebug("Feature flags cache HIT for tenant {TenantId}", tenantId);
                return features;
            }

            _logger.LogDebug("Feature flags cache MISS for tenant {TenantId}", tenantId);
            return null;
        }

        public void Set(Guid tenantId, TenantFeatureFlags features)
        {
            var cacheKey = GetCacheKey(tenantId);
            
            _cache.Set(cacheKey, features, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = AbsoluteExpiration,
                SlidingExpiration = SlidingExpiration
            });

            _logger.LogDebug("Feature flags cached for tenant {TenantId} (Absolute: {Absolute}, Sliding: {Sliding})", 
                tenantId, AbsoluteExpiration, SlidingExpiration);
        }

        public void Invalidate(Guid tenantId)
        {
            var cacheKey = GetCacheKey(tenantId);
            _cache.Remove(cacheKey);
            _logger.LogInformation("Feature flags cache invalidated for tenant {TenantId}", tenantId);
        }

        public void Clear()
        {
            // En producción, considerar usar un prefijo común y un registro de keys
            _logger.LogWarning("FeatureCache.Clear() called - consider implementing cache key tracking for production");
        }

        private static string GetCacheKey(Guid tenantId)
        {
            return $"FeatureFlags_Tenant_{tenantId}";
        }
    }
}
