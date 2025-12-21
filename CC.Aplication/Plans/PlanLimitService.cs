using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CC.Aplication.Plans
{
    /// <summary>
    /// Servicio para validar y obtener l�mites del plan del tenant
    /// </summary>
    public interface IPlanLimitService
    {
        Task<int> GetLimitValueAsync(string limitCode);
        Task<bool> CanExceedLimitAsync(string limitCode, int currentValue);
        Task ThrowIfExceedsLimitAsync(string limitCode, int currentValue, string? customMessage = null);
        Task<Dictionary<string, int>> GetAllLimitsAsync();
        Task<PlanStatusDto> GetPlanStatusAsync();
    }

    public class PlanLimitService : IPlanLimitService
    {
        private readonly AdminDbContext _adminDb;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly TenantDbContextFactory _tenantDbFactory;

        public PlanLimitService(
            AdminDbContext adminDb,
            ITenantAccessor tenantAccessor,
            TenantDbContextFactory tenantDbFactory)
        {
            _adminDb = adminDb;
            _tenantAccessor = tenantAccessor;
            _tenantDbFactory = tenantDbFactory;
        }

        /// <summary>
        /// Obtiene el valor del l�mite para el tenant actual
        /// Retorna -1 si es ilimitado, 0 si est� bloqueado
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

            return limit?.LimitValue ?? 0; // Si no existe el l�mite, retornar 0 (bloqueado)
        }

        /// <summary>
        /// Verifica si el tenant puede exceder el l�mite
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

            // Verificar si el valor actual est� dentro del l�mite
            return currentValue < limitValue;
        }

        /// <summary>
        /// Lanza excepci�n si se excede el l�mite
        /// </summary>
        public async Task ThrowIfExceedsLimitAsync(string limitCode, int currentValue, string? customMessage = null)
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

        /// <summary>
        /// Obtiene el estado completo del plan: l�mites, uso actual y si hay excesos
        /// Ideal para que el frontend muestre el dashboard de uso
        /// </summary>
        public async Task<PlanStatusDto> GetPlanStatusAsync()
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            // Obtener tenant con su plan y l�mites
            var tenant = await _adminDb.Tenants
                .Include(t => t.Plan)
                    .ThenInclude(p => p!.Limits)
                .FirstOrDefaultAsync(t => t.Id == _tenantAccessor.TenantInfo.Id);

            if (tenant?.Plan == null)
            {
                throw new InvalidOperationException("Tenant does not have a plan assigned");
            }

            // Obtener uso actual de la DB del tenant
            await using var tenantDb = _tenantDbFactory.Create();

            var usage = new Dictionary<string, int>
            {
                [PlanLimitCodes.MaxProducts] = await tenantDb.Products.CountAsync(),
                [PlanLimitCodes.MaxCategories] = await tenantDb.Categories.CountAsync(),
                [PlanLimitCodes.MaxAdminUsers] = await tenantDb.Users.CountAsync(),
                // Agregar m�s m�tricas seg�n necesites
            };

            // Construir lista de l�mites con estado
            var limitStatuses = new List<LimitStatusDto>();

            foreach (var limit in tenant.Plan.Limits)
            {
                var currentUsage = usage.GetValueOrDefault(limit.LimitCode, 0);
                var isExceeded = limit.LimitValue != -1 && currentUsage >= limit.LimitValue;
                var isBlocked = limit.LimitValue == 0;
                var percentUsed = limit.LimitValue > 0
                    ? Math.Round((double)currentUsage / limit.LimitValue * 100, 1)
                    : (limit.LimitValue == -1 ? 0 : 100);

                limitStatuses.Add(new LimitStatusDto
                {
                    LimitCode = limit.LimitCode,
                    LimitValue = limit.LimitValue,
                    CurrentUsage = currentUsage,
                    IsExceeded = isExceeded,
                    IsBlocked = isBlocked,
                    PercentUsed = percentUsed,
                    Description = limit.Description,
                    DisplayValue = limit.LimitValue == -1 ? "Ilimitado" : limit.LimitValue.ToString()
                });
            }

            var hasExceededLimits = limitStatuses.Any(l => l.IsExceeded);

            return new PlanStatusDto
            {
                PlanCode = tenant.Plan.Code,
                PlanName = tenant.Plan.Name,
                Limits = limitStatuses,
                HasExceededLimits = hasExceededLimits,
                Message = hasExceededLimits
                    ? "⚠️ Has excedido algunos l�mites de tu plan. Las operaciones de creaci�n/edici�n est�n bloqueadas para esos recursos."
                    : "✅ Est�s dentro de los l�mites de tu plan."
            };
        }
    }

    /// <summary>
    /// Excepci�n personalizada cuando se excede un l�mite del plan
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

    // ==================== DTOs ====================

    /// <summary>
    /// Estado completo del plan del tenant
    /// </summary>
    public class PlanStatusDto
    {
        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public List<LimitStatusDto> Limits { get; set; } = new();
        public bool HasExceededLimits { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Estado individual de un l�mite
    /// </summary>
    public class LimitStatusDto
    {
        public string LimitCode { get; set; } = string.Empty;
        public int LimitValue { get; set; }
        public int CurrentUsage { get; set; }
        public bool IsExceeded { get; set; }
        public bool IsBlocked { get; set; }
        public double PercentUsed { get; set; }
        public string? Description { get; set; }
        public string DisplayValue { get; set; } = string.Empty;
    }
}
