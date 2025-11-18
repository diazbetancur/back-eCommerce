using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CC.Aplication.Plans
{
    /// <summary>
    /// Servicio para validar y obtener límites del plan del tenant
    /// </summary>
    public interface IPlanLimitService
    {
        Task<int> GetLimitValueAsync(string limitCode);
        Task<bool> CanExceedLimitAsync(string limitCode, int currentValue);
        Task ThrowIfExceedsLimitAsync(string limitCode, int currentValue, string? customMessage = null);
        Task<Dictionary<string, int>> GetAllLimitsAsync();
    }

    public class PlanLimitService : IPlanLimitService
    {
        private readonly AdminDbContext _adminDb;
        private readonly ITenantAccessor _tenantAccessor;

        public PlanLimitService(AdminDbContext adminDb, ITenantAccessor tenantAccessor)
        {
            _adminDb = adminDb;
            _tenantAccessor = tenantAccessor;
        }

        /// <summary>
        /// Obtiene el valor del límite para el tenant actual
        /// Retorna -1 si es ilimitado, 0 si está bloqueado
        /// </summary>
        public async Task<int> GetLimitValueAsync(string limitCode)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            var tenant = await _adminDb.Tenants
                .Include(t => t.Plan)
                    .ThenInclude(p => p!.Limits)
                .FirstOrDefaultAsync(t => t.Id == _tenantAccessor.TenantInfo.Id);

            if (tenant?.Plan == null)
            {
                throw new InvalidOperationException("Tenant does not have a plan assigned");
            }

            var limit = tenant.Plan.Limits.FirstOrDefault(l => l.LimitCode == limitCode);
            
            return limit?.LimitValue ?? 0; // Si no existe el límite, retornar 0 (bloqueado)
        }

        /// <summary>
        /// Verifica si el tenant puede exceder el límite
        /// </summary>
        public async Task<bool> CanExceedLimitAsync(string limitCode, int currentValue)
        {
            var limitValue = await GetLimitValueAsync(limitCode);

            // -1 = ilimitado
            if (limitValue == -1)
            {
                return true;
            }

            // 0 = bloqueado
            if (limitValue == 0)
            {
                return false;
            }

            // Verificar si el valor actual está dentro del límite
            return currentValue < limitValue;
        }

        /// <summary>
        /// Lanza excepción si se excede el límite
        /// </summary>
        public async Task ThrowIfExceedsLimitAsync(string limitCode, int currentValue, string? customMessage = null)
        {
            var limitValue = await GetLimitValueAsync(limitCode);

            // -1 = ilimitado, no hay restricción
            if (limitValue == -1)
            {
                return;
            }

            // 0 = bloqueado completamente
            if (limitValue == 0)
            {
                throw new PlanLimitExceededException(
                    limitCode,
                    limitValue,
                    currentValue,
                    customMessage ?? $"Esta funcionalidad está bloqueada en tu plan actual"
                );
            }

            // Verificar si se excede el límite
            if (currentValue >= limitValue)
            {
                throw new PlanLimitExceededException(
                    limitCode,
                    limitValue,
                    currentValue,
                    customMessage ?? $"Has alcanzado el límite de tu plan ({limitValue}). Mejora tu plan para continuar."
                );
            }
        }

        /// <summary>
        /// Obtiene todos los límites del plan del tenant actual
        /// </summary>
        public async Task<Dictionary<string, int>> GetAllLimitsAsync()
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            var tenant = await _adminDb.Tenants
                .Include(t => t.Plan)
                    .ThenInclude(p => p!.Limits)
                .FirstOrDefaultAsync(t => t.Id == _tenantAccessor.TenantInfo.Id);

            if (tenant?.Plan == null)
            {
                return new Dictionary<string, int>();
            }

            return tenant.Plan.Limits.ToDictionary(
                l => l.LimitCode,
                l => l.LimitValue
            );
        }
    }

    /// <summary>
    /// Excepción personalizada cuando se excede un límite del plan
    /// </summary>
    public class PlanLimitExceededException : Exception
    {
        public string LimitCode { get; }
        public int LimitValue { get; }
        public int CurrentValue { get; }

        public PlanLimitExceededException(
            string limitCode,
            int limitValue,
            int currentValue,
            string message) : base(message)
        {
            LimitCode = limitCode;
            LimitValue = limitValue;
            CurrentValue = currentValue;
        }
    }
}
