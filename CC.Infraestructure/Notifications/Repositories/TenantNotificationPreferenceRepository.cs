using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Notifications.Repositories;

public sealed class TenantNotificationPreferenceRepository : ITenantNotificationPreferenceRepository
{
  private readonly AdminDbContext _adminDb;

  public TenantNotificationPreferenceRepository(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public Task<TenantNotificationPreference?> GetAsync(Guid tenantId, string eventCode, NotificationChannel channel, CancellationToken ct = default)
  {
    return _adminDb.TenantNotificationPreferences
        .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.EventCode == eventCode && item.Channel == channel, ct);
  }

  public Task<List<TenantNotificationPreference>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
  {
    return _adminDb.TenantNotificationPreferences
        .Where(item => item.TenantId == tenantId)
        .ToListAsync(ct);
  }

  public void Add(TenantNotificationPreference preference)
  {
    _adminDb.TenantNotificationPreferences.Add(preference);
  }

  public void AddRange(IEnumerable<TenantNotificationPreference> preferences)
  {
    _adminDb.TenantNotificationPreferences.AddRange(preferences);
  }

  public void Update(TenantNotificationPreference preference)
  {
    _adminDb.TenantNotificationPreferences.Update(preference);
  }
}