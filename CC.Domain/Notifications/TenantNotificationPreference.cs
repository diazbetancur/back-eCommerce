namespace CC.Domain.Notifications;

public class TenantNotificationPreference
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid TenantId { get; set; }
  public string EventCode { get; set; } = string.Empty;
  public NotificationChannel Channel { get; set; }
  public bool IsEnabled { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? UpdatedAt { get; set; }
}