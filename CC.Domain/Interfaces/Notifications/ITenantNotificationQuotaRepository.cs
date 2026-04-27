using CC.Domain.Notifications;

namespace CC.Domain.Interfaces.Notifications;

public interface ITenantNotificationQuotaRepository
{
  Task<TenantNotificationQuota?> GetByTenantAndPeriodAsync(Guid tenantId, int periodYear, int periodMonth, CancellationToken ct = default);
  void Add(TenantNotificationQuota quota);
  void Update(TenantNotificationQuota quota);
  Task<bool> TryConsumeEmailCreditAsync(Guid tenantId, int periodYear, int periodMonth, CancellationToken ct = default);
}