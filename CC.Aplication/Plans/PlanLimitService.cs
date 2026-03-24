using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CC.Aplication.Plans
{
    /// <summary>
    /// Servicio para validar y obtener l�mites del plan del tenant
    /// </summary>
    public interface IPlanLimitService
    {
        Task<long> GetLimitValueAsync(string limitCode);
        Task<bool> CanExceedLimitAsync(string limitCode, long currentValue);
        Task ThrowIfExceedsLimitAsync(string limitCode, long currentValue, string? customMessage = null);
        Task<Dictionary<string, long>> GetAllLimitsAsync();
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
        /// Obtiene el valor del l�mite para el tenant actual
        /// Retorna -1 si es ilimitado, 0 si est� bloqueado
        /// </summary>
        public async Task<long> GetLimitValueAsync(string limitCode)
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

            return limit?.LimitValue ?? 0; // Si no existe el l�mite, retornar 0 (bloqueado)
        }

        /// <summary>
        /// Verifica si el tenant puede exceder el l�mite
        /// </summary>
        public async Task<bool> CanExceedLimitAsync(string limitCode, long currentValue)
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

            // Verificar si el valor actual est� dentro del l�mite
            return currentValue < limitValue;
        }

        /// <summary>
        /// Lanza excepci�n si se excede el l�mite
        /// </summary>
        public async Task ThrowIfExceedsLimitAsync(string limitCode, long currentValue, string? customMessage = null)
        {
            var limitValue = await GetLimitValueAsync(limitCode);

            // -1 = ilimitado, no hay restricci�n
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
                    customMessage ?? $"Esta funcionalidad est� bloqueada en tu plan actual"
                );
            }

            // Verificar si se excede el l�mite
            if (currentValue >= limitValue)
            {
                throw new PlanLimitExceededException(
                    limitCode,
                    limitValue,
                    currentValue,
                    customMessage ?? $"Has alcanzado el l�mite de tu plan ({limitValue}). Mejora tu plan para continuar."
                );
            }
        }

        /// <summary>
        /// Obtiene todos los l�mites del plan del tenant actual
        /// </summary>
        public async Task<Dictionary<string, long>> GetAllLimitsAsync()
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
                return new Dictionary<string, long>();
            }

            return tenant.Plan.Limits.ToDictionary(
                l => l.LimitCode,
                l => l.LimitValue
            );
        }
    }

    /// <summary>
    /// Excepci�n personalizada cuando se excede un l�mite del plan
    /// </summary>
    public class PlanLimitExceededException : Exception
    {
        public string LimitCode { get; }
        public long LimitValue { get; }
        public long CurrentValue { get; }

        public PlanLimitExceededException(
            string limitCode,
            long limitValue,
            long currentValue,
            string message) : base(message)
        {
            LimitCode = limitCode;
            LimitValue = limitValue;
            CurrentValue = currentValue;
        }
    }
}
