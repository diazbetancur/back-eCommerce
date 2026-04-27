using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Notifications.Repositories;

public sealed class NotificationTenantPlanRepository : INotificationTenantPlanRepository
{
  private readonly AdminDbContext _adminDb;

  public NotificationTenantPlanRepository(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public async Task<int> GetIncludedEmailCreditsAsync(Guid tenantId, CancellationToken ct = default)
  {
    var limitValue = await _adminDb.Tenants
        .Where(item => item.Id == tenantId && item.PlanId != null)
        .SelectMany(item => item.Plan!.Limits)
        .Where(item => item.LimitCode == NotificationPlanLimitCodes.IncludedEmailCreditsPerMonth)
        .Select(item => item.LimitValue)
        .FirstOrDefaultAsync(ct);

    return (int)Math.Max(0L, limitValue);
  }
}