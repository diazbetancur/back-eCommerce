using CC.Infraestructure.Admin;
using CC.Infraestructure.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Admin
{
    public interface IAdminTenantService
    {
        Task<PagedTenantsResponse> GetTenantsAsync(TenantListQuery query, CancellationToken ct = default);
        Task<TenantDetailDto> GetTenantByIdAsync(Guid tenantId, CancellationToken ct = default);
        Task<TenantDetailDto> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request, CancellationToken ct = default);
        Task<TenantDetailDto> UpdateTenantStatusAsync(Guid tenantId, UpdateTenantStatusRequest request, CancellationToken ct = default);
        Task DeleteTenantAsync(Guid tenantId, CancellationToken ct = default);
    }

    public class AdminTenantService : IAdminTenantService
    {
        private readonly AdminDbContext _adminDb;
        private readonly ILogger<AdminTenantService> _logger;

        public AdminTenantService(
            AdminDbContext adminDb,
            ILogger<AdminTenantService> logger)
        {
            _adminDb = adminDb;
            _logger = logger;
        }

        public async Task<PagedTenantsResponse> GetTenantsAsync(TenantListQuery query, CancellationToken ct = default)
        {
            var tenantsQuery = _adminDb.Tenants
                .Include(t => t.Plan)
                .AsNoTracking()
                .AsQueryable();

            // Filtros
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.ToLower();
                tenantsQuery = tenantsQuery.Where(t =>
                    t.Name.ToLower().Contains(search) ||
                    t.Slug.ToLower().Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                if (Enum.TryParse<TenantStatus>(query.Status, true, out var status))
                {
                    tenantsQuery = tenantsQuery.Where(t => t.Status == status);
                }
            }

            if (query.PlanId.HasValue)
            {
                tenantsQuery = tenantsQuery.Where(t => t.PlanId == query.PlanId.Value);
            }

            // Contar total
            var totalCount = await tenantsQuery.CountAsync(ct);

            // Paginación
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var page = Math.Max(query.Page, 1);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Obtener items
            var tenants = await tenantsQuery
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = tenants.Select(t => new TenantSummaryDto(
                t.Id,
                t.Slug,
                t.Name,
                t.DbName,
                t.Status.ToString(),
                t.Plan?.Name,
                t.CreatedAt,
                t.UpdatedAt,
                t.LastError
            )).ToList();

            return new PagedTenantsResponse(items, totalCount, page, pageSize, totalPages);
        }

        public async Task<TenantDetailDto> GetTenantByIdAsync(Guid tenantId, CancellationToken ct = default)
        {
            var tenant = await _adminDb.Tenants
                .Include(t => t.Plan)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

            if (tenant == null)
            {
                throw new InvalidOperationException($"Tenant {tenantId} not found");
            }

            // Obtener últimos pasos de provisioning
            var recentSteps = await _adminDb.TenantProvisionings
                .Where(tp => tp.TenantId == tenantId)
                .OrderByDescending(tp => tp.StartedAt)
                .Take(10)
                .Select(tp => new ProvisioningStepDto(
                    tp.Id,
                    tp.Step,
                    tp.Status,
                    tp.StartedAt,
                    tp.CompletedAt,
                    tp.Message // ? Usar Message en lugar de Log
                ))
                .ToListAsync(ct);

            return new TenantDetailDto(
                tenant.Id,
                tenant.Slug,
                tenant.Name,
                tenant.DbName,
                tenant.Status.ToString(),
                tenant.PlanId,
                tenant.Plan?.Name,
                tenant.FeatureFlagsJson,
                tenant.AllowedOrigins,
                tenant.CreatedAt,
                tenant.UpdatedAt,
                tenant.LastError,
                recentSteps
            );
        }

        public async Task<TenantDetailDto> UpdateTenantAsync(
            Guid tenantId,
            UpdateTenantRequest request,
            CancellationToken ct = default)
        {
            var tenant = await _adminDb.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

            if (tenant == null)
            {
                throw new InvalidOperationException($"Tenant {tenantId} not found");
            }

            // Actualizar campos
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                tenant.Name = request.Name;
            }

            if (request.PlanId.HasValue)
            {
                // Verificar que el plan existe
                var planExists = await _adminDb.Plans.AnyAsync(p => p.Id == request.PlanId.Value, ct);
                if (!planExists)
                {
                    throw new InvalidOperationException($"Plan {request.PlanId} not found");
                }
                tenant.PlanId = request.PlanId.Value;
            }

            if (request.FeatureFlagsJson != null)
            {
                tenant.FeatureFlagsJson = request.FeatureFlagsJson;
            }

            if (request.AllowedOrigins != null)
            {
                tenant.AllowedOrigins = request.AllowedOrigins;
            }

            if (request.IsActive.HasValue)
            {
                tenant.Status = request.IsActive.Value ? TenantStatus.Ready : TenantStatus.Suspended;
            }

            tenant.UpdatedAt = DateTime.UtcNow;

            await _adminDb.SaveChangesAsync(ct);

            _logger.LogInformation("Tenant {TenantId} updated by admin", tenantId);

            return await GetTenantByIdAsync(tenantId, ct);
        }

        public async Task<TenantDetailDto> UpdateTenantStatusAsync(
            Guid tenantId,
            UpdateTenantStatusRequest request,
            CancellationToken ct = default)
        {
            var tenant = await _adminDb.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

            if (tenant == null)
            {
                throw new InvalidOperationException($"Tenant {tenantId} not found");
            }

            if (Enum.TryParse<TenantStatus>(request.Status, true, out var status))
            {
                tenant.Status = status;
                tenant.UpdatedAt = DateTime.UtcNow;

                await _adminDb.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Tenant {TenantId} status changed to {Status}",
                    tenantId, status);
            }
            else
            {
                throw new InvalidOperationException($"Invalid status: {request.Status}");
            }

            return await GetTenantByIdAsync(tenantId, ct);
        }

        public async Task DeleteTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            var tenant = await _adminDb.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

            if (tenant == null)
            {
                throw new InvalidOperationException($"Tenant {tenantId} not found");
            }

            _adminDb.Tenants.Remove(tenant);
            await _adminDb.SaveChangesAsync(ct);

            _logger.LogWarning("Tenant {TenantId} ({Slug}) deleted by admin", tenantId, tenant.Slug);
        }
    }
}
