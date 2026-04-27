using CC.Domain.Notifications;

namespace CC.Aplication.Notifications;

public sealed class NotificationEligibilityResult
{
  public bool CanSend { get; init; }
  public NotificationDeliveryStatus? SkipReason { get; init; }
  public bool ConsumesQuota { get; init; }
  public NotificationEventDefinition? EventDefinition { get; init; }
  public NotificationTemplate? Template { get; init; }
  public string? FailureMessage { get; init; }
}