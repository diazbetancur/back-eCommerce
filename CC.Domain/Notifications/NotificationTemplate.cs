namespace CC.Domain.Notifications;

public class NotificationTemplate
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Code { get; set; } = string.Empty;
  public NotificationChannel Channel { get; set; }
  public NotificationSourceType SourceType { get; set; } = NotificationSourceType.Platform;
  public string Name { get; set; } = string.Empty;
  public string? SubjectTemplate { get; set; }
  public string? HtmlTemplate { get; set; }
  public string? TextTemplate { get; set; }
  public string AvailableVariablesJson { get; set; } = "[]";
  public int Version { get; set; } = 1;
  public bool IsActive { get; set; } = true;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? UpdatedAt { get; set; }
}