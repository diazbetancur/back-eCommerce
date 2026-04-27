using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Notifications.Repositories;

public sealed class TenantNotificationCreditLedgerRepository : ITenantNotificationCreditLedgerRepository
{
  private readonly AdminDbContext _adminDb;

  public TenantNotificationCreditLedgerRepository(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public Task<bool> ExistsMonthlyGrantAsync(Guid tenantId, NotificationChannel channel, int periodYear, int periodMonth, CancellationToken ct = default)
  {
    return _adminDb.TenantNotificationCreditLedgers
        .AsNoTracking()
        .AnyAsync(item => item.TenantId == tenantId
            && item.Channel == channel
            && item.PeriodYear == periodYear
            && item.PeriodMonth == periodMonth
            && item.MovementType == NotificationCreditMovementType.MonthlyPlanGrant,
            ct);
  }

  public void Add(TenantNotificationCreditLedger movement)
  {
    _adminDb.TenantNotificationCreditLedgers.Add(movement);
  }
}