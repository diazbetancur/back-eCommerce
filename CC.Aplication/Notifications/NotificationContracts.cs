using CC.Domain.Notifications;

namespace CC.Aplication.Notifications;

public interface INotificationPreferenceService
{
  Task InitializeTenantPreferencesAsync(Guid tenantId, CancellationToken ct = default);
  Task<bool> IsEnabledAsync(Guid tenantId, string eventCode, NotificationChannel channel, CancellationToken ct = default);
  Task<TenantNotificationPreference> SetPreferenceAsync(Guid tenantId, string eventCode, NotificationChannel channel, bool isEnabled, CancellationToken ct = default);
}

public interface INotificationQuotaService
{
  Task<TenantNotificationQuota> GetOrCreateCurrentMonthlyQuotaAsync(Guid tenantId, CancellationToken ct = default);
  Task<TenantNotificationQuota> GrantMonthlyPlanCreditsAsync(Guid tenantId, CancellationToken ct = default);
  Task<TenantNotificationQuota> AddPurchasedCreditsForCurrentMonthAsync(Guid tenantId, int quantity, string reason, string? referenceType = null, string? referenceId = null, CancellationToken ct = default);
  Task<bool> TryConsumeEmailCreditAsync(Guid tenantId, string reason, string? referenceType = null, string? referenceId = null, CancellationToken ct = default);
  Task<int> GetAvailableCreditsAsync(Guid tenantId, CancellationToken ct = default);
}

public interface INotificationEligibilityService
{
  Task<NotificationEligibilityResult> EvaluateAsync(Guid? tenantId, string eventCode, NotificationChannel channel, string recipient, CancellationToken ct = default);
}

public interface INotificationDispatcher
{
  Task<NotificationDispatchResult> DispatchAsync(NotificationDispatchRequest request, CancellationToken ct = default);
}