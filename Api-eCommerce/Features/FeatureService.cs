using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;

namespace Api_eCommerce.Features
{
 public class FeatureService : IFeatureService
 {
 private readonly AdminDbContext _adminDb;
 public FeatureService(AdminDbContext adminDb) { _adminDb = adminDb; }

 public async Task<bool> IsEnabledAsync(string tenantSlug, string featureCode)
 {
 var tenant = await _adminDb.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == tenantSlug);
 if (tenant == null || tenant.PlanId == null) return false;
 var planEnabled = await _adminDb.PlanFeatures
 .Include(pf => pf.Feature)
 .Where(pf => pf.PlanId == tenant.PlanId && pf.Feature.Code == featureCode)
 .Select(pf => pf.Enabled)
 .FirstOrDefaultAsync();
 var overrideEnabled = await _adminDb.TenantFeatureOverrides
 .Include(o => o.Feature)
 .Where(o => o.TenantId == tenant.Id && o.Feature.Code == featureCode)
 .Select(o => o.Enabled)
 .FirstOrDefaultAsync();
 return overrideEnabled ?? planEnabled;
 }

 public async Task<int?> GetLimitAsync(string tenantSlug, string featureCode)
 {
 var tenant = await _adminDb.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == tenantSlug);
 if (tenant == null || tenant.PlanId == null) return null;
 var planLimit = await _adminDb.PlanFeatures
 .Include(pf => pf.Feature)
 .Where(pf => pf.PlanId == tenant.PlanId && pf.Feature.Code == featureCode)
 .Select(pf => pf.LimitValue)
 .FirstOrDefaultAsync();
 var overrideLimit = await _adminDb.TenantFeatureOverrides
 .Include(o => o.Feature)
 .Where(o => o.TenantId == tenant.Id && o.Feature.Code == featureCode)
 .Select(o => o.LimitValue)
 .FirstOrDefaultAsync();
 return overrideLimit ?? planLimit;
 }
}}