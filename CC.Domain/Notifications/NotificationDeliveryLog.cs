namespace CC.Domain.Notifications;

public class NotificationDeliveryLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid? TenantId { get; set; }
  public string EventCode { get; set; } = string.Empty;
  public string? TemplateCode { get; set; }
  public NotificationChannel Channel { get; set; }
  public string Recipient { get; set; } = string.Empty;
  public string? FromEmail { get; set; }
  public string? FromName { get; set; }
  public string? ReplyTo { get; set; }
  public string? Subject { get; set; }
  public NotificationDeliveryStatus Status { get; set; }
  public string? Provider { get; set; }
  public string? ProviderMessageId { get; set; }
  public string? ErrorCode { get; set; }
  public string? ErrorMessage { get; set; }
  public int ConsumedCredits { get; set; }
  public string? ReferenceType { get; set; }
  public string? ReferenceId { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? SentAt { get; set; }
}