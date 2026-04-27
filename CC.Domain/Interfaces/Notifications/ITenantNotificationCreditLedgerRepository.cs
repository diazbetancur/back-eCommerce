using CC.Domain.Notifications;

namespace CC.Domain.Interfaces.Notifications;

public interface ITenantNotificationCreditLedgerRepository
{
  Task<bool> ExistsMonthlyGrantAsync(Guid tenantId, NotificationChannel channel, int periodYear, int periodMonth, CancellationToken ct = default);
  void Add(TenantNotificationCreditLedger movement);
}