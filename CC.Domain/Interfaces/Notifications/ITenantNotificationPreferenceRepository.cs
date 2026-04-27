using CC.Domain.Notifications;

namespace CC.Domain.Interfaces.Notifications;

public interface ITenantNotificationPreferenceRepository
{
  Task<TenantNotificationPreference?> GetAsync(Guid tenantId, string eventCode, NotificationChannel channel, CancellationToken ct = default);
  Task<List<TenantNotificationPreference>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
  void Add(TenantNotificationPreference preference);
  void AddRange(IEnumerable<TenantNotificationPreference> preferences);
  void Update(TenantNotificationPreference preference);
}