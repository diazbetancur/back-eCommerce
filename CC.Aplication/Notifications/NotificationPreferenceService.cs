using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;

namespace CC.Aplication.Notifications;

public sealed class NotificationPreferenceService : INotificationPreferenceService
{
  private readonly INotificationEventDefinitionRepository _eventDefinitions;
  private readonly ITenantNotificationPreferenceRepository _preferences;
  private readonly INotificationUnitOfWork _unitOfWork;

  public NotificationPreferenceService(
      INotificationEventDefinitionRepository eventDefinitions,
      ITenantNotificationPreferenceRepository preferences,
      INotificationUnitOfWork unitOfWork)
  {
    _eventDefinitions = eventDefinitions;
    _preferences = preferences;
    _unitOfWork = unitOfWork;
  }

  public async Task InitializeTenantPreferencesAsync(Guid tenantId, CancellationToken ct = default)
  {
    var definitions = await _eventDefinitions.GetActiveByChannelAsync(NotificationChannel.Email, ct);
    var existing = await _preferences.GetByTenantAsync(tenantId, ct);
    var existingKeys = existing
        .Select(item => $"{item.EventCode}:{item.Channel}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var missing = definitions
        .Where(item => !existingKeys.Contains($"{item.Code}:{item.Channel}"))
        .Select(item => new TenantNotificationPreference
        {
          TenantId = tenantId,
          EventCode = item.Code,
          Channel = item.Channel,
          IsEnabled = item.IsSystemRequired || item.DefaultEnabled,
          CreatedAt = DateTime.UtcNow
        })
        .ToList();

    if (missing.Count == 0)
    {
      return;
    }

    _preferences.AddRange(missing);
    await _unitOfWork.SaveChangesAsync(ct);
  }

  public async Task<bool> IsEnabledAsync(Guid tenantId, string eventCode, NotificationChannel channel, CancellationToken ct = default)
  {
    await InitializeTenantPreferencesAsync(tenantId, ct);
    var preference = await _preferences.GetAsync(tenantId, eventCode, channel, ct);
    return preference?.IsEnabled ?? false;
  }

  public async Task<TenantNotificationPreference> SetPreferenceAsync(Guid tenantId, string eventCode, NotificationChannel channel, bool isEnabled, CancellationToken ct = default)
  {
    var definition = await _eventDefinitions.GetByCodeAsync(eventCode, channel, ct)
        ?? throw new InvalidOperationException($"Notification event '{eventCode}' was not found.");

    if (definition.IsSystemRequired)
    {
      throw new InvalidOperationException($"Notification event '{eventCode}' is system required and cannot be disabled.");
    }

    if (!definition.IsTenantConfigurable)
    {
      throw new InvalidOperationException($"Notification event '{eventCode}' is not tenant configurable.");
    }

    await InitializeTenantPreferencesAsync(tenantId, ct);
    var preference = await _preferences.GetAsync(tenantId, eventCode, channel, ct)
        ?? throw new InvalidOperationException($"Notification preference '{eventCode}' was not found for tenant '{tenantId}'.");

    preference.IsEnabled = isEnabled;
    preference.UpdatedAt = DateTime.UtcNow;

    _preferences.Update(preference);
    await _unitOfWork.SaveChangesAsync(ct);

    return preference;
  }
}