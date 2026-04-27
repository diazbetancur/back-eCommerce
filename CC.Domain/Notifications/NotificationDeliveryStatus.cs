namespace CC.Domain.Notifications;

public enum NotificationDeliveryStatus
{
  Pending = 1,
  Sent = 2,
  Failed = 3,
  SkippedDisabled = 4,
  SkippedQuotaExceeded = 5,
  SkippedMissingTemplate = 6,
  SkippedInvalidRecipient = 7,
  SkippedSystemRule = 8
}