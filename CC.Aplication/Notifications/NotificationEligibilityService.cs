using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;

namespace CC.Aplication.Notifications;

public sealed class NotificationEligibilityService : INotificationEligibilityService
{
  private readonly INotificationEventDefinitionRepository _eventDefinitions;
  private readonly INotificationTemplateRepository _templates;
  private readonly INotificationPreferenceService _preferences;
  private readonly INotificationQuotaService _quotaService;

  public NotificationEligibilityService(
      INotificationEventDefinitionRepository eventDefinitions,
      INotificationTemplateRepository templates,
      INotificationPreferenceService preferences,
      INotificationQuotaService quotaService)
  {
    _eventDefinitions = eventDefinitions;
    _templates = templates;
    _preferences = preferences;
    _quotaService = quotaService;
  }

  public async Task<NotificationEligibilityResult> EvaluateAsync(Guid? tenantId, string eventCode, NotificationChannel channel, string recipient, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(recipient) || !recipient.Contains('@', StringComparison.Ordinal))
    {
      return new NotificationEligibilityResult
      {
        CanSend = false,
        SkipReason = NotificationDeliveryStatus.SkippedInvalidRecipient,
        FailureMessage = "Recipient is invalid."
      };
    }

    var eventDefinition = await _eventDefinitions.GetByCodeAsync(eventCode, channel, ct);
    if (eventDefinition == null || !eventDefinition.IsActive)
    {
      return new NotificationEligibilityResult
      {
        CanSend = false,
        SkipReason = NotificationDeliveryStatus.SkippedSystemRule,
        FailureMessage = "Notification event is not active.",
        EventDefinition = eventDefinition
      };
    }

    var template = await _templates.GetActiveByCodeAsync(eventDefinition.TemplateCode, channel, ct);
    if (template == null)
    {
      return new NotificationEligibilityResult
      {
        CanSend = false,
        SkipReason = NotificationDeliveryStatus.SkippedMissingTemplate,
        FailureMessage = "Notification template is missing.",
        EventDefinition = eventDefinition
      };
    }

    if (tenantId.HasValue)
    {
      await _preferences.InitializeTenantPreferencesAsync(tenantId.Value, ct);

      if (!eventDefinition.IsSystemRequired && eventDefinition.IsTenantConfigurable)
      {
        var enabled = await _preferences.IsEnabledAsync(tenantId.Value, eventCode, channel, ct);
        if (!enabled)
        {
          return new NotificationEligibilityResult
          {
            CanSend = false,
            SkipReason = NotificationDeliveryStatus.SkippedDisabled,
            FailureMessage = "Notification event is disabled for tenant.",
            ConsumesQuota = eventDefinition.ConsumesQuota,
            EventDefinition = eventDefinition,
            Template = template
          };
        }
      }

      if (eventDefinition.ConsumesQuota)
      {
        var availableCredits = await _quotaService.GetAvailableCreditsAsync(tenantId.Value, ct);
        if (availableCredits <= 0)
        {
          return new NotificationEligibilityResult
          {
            CanSend = false,
            SkipReason = NotificationDeliveryStatus.SkippedQuotaExceeded,
            FailureMessage = "Notification quota exceeded.",
            ConsumesQuota = true,
            EventDefinition = eventDefinition,
            Template = template
          };
        }
      }
    }

    return new NotificationEligibilityResult
    {
      CanSend = true,
      ConsumesQuota = eventDefinition.ConsumesQuota && tenantId.HasValue,
      EventDefinition = eventDefinition,
      Template = template
    };
  }
}