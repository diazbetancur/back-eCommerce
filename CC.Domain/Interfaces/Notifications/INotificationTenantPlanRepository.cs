namespace CC.Domain.Interfaces.Notifications;

public interface INotificationTenantPlanRepository
{
  Task<int> GetIncludedEmailCreditsAsync(Guid tenantId, CancellationToken ct = default);
}