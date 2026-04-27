namespace CC.Domain.Notifications;

public class NotificationEventDefinition
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Code { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }
  public NotificationCategory Category { get; set; }
  public NotificationChannel Channel { get; set; }
  public bool IsTenantConfigurable { get; set; }
  public bool IsSystemRequired { get; set; }
  public bool ConsumesQuota { get; set; }
  public bool DefaultEnabled { get; set; }
  public string TemplateCode { get; set; } = string.Empty;
  public bool IsActive { get; set; } = true;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? UpdatedAt { get; set; }
}