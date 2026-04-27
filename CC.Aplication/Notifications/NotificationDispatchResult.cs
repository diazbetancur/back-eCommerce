using CC.Domain.Notifications;

namespace CC.Aplication.Notifications;

public sealed class NotificationDispatchResult
{
  public bool Accepted { get; init; }
  public NotificationDeliveryStatus Status { get; init; }
  public Guid DeliveryLogId { get; init; }
  public bool ConsumedQuota { get; init; }
  public string? ProviderMessageId { get; init; }
  public string? Message { get; init; }
}