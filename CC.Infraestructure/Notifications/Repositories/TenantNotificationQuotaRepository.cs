using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Notifications.Repositories;

public sealed class TenantNotificationQuotaRepository : ITenantNotificationQuotaRepository
{
  private readonly AdminDbContext _adminDb;

  public TenantNotificationQuotaRepository(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public Task<TenantNotificationQuota?> GetByTenantAndPeriodAsync(Guid tenantId, int periodYear, int periodMonth, CancellationToken ct = default)
  {
    return _adminDb.TenantNotificationQuotas
        .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.PeriodYear == periodYear && item.PeriodMonth == periodMonth, ct);
  }

  public void Add(TenantNotificationQuota quota)
  {
    _adminDb.TenantNotificationQuotas.Add(quota);
  }

  public void Update(TenantNotificationQuota quota)
  {
    _adminDb.TenantNotificationQuotas.Update(quota);
  }

  public async Task<bool> TryConsumeEmailCreditAsync(Guid tenantId, int periodYear, int periodMonth, CancellationToken ct = default)
  {
    var affectedRows = await _adminDb.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE admin.""TenantNotificationQuotas""
SET ""UsedEmailCredits"" = ""UsedEmailCredits"" + 1,
    ""UpdatedAt"" = {DateTime.UtcNow}
WHERE ""TenantId"" = {tenantId}
  AND ""PeriodYear"" = {periodYear}
  AND ""PeriodMonth"" = {periodMonth}
  AND (""IncludedEmailCredits"" + ""PurchasedEmailCredits"" - ""UsedEmailCredits"" - ""ReservedEmailCredits"") > 0;", ct);

    return affectedRows > 0;
  }
}