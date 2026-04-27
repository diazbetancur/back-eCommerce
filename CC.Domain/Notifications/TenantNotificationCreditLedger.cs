namespace CC.Domain.Notifications;

public class TenantNotificationCreditLedger
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid TenantId { get; set; }
  public NotificationChannel Channel { get; set; }
  public NotificationCreditMovementType MovementType { get; set; }
  public int Quantity { get; set; }
  public string Reason { get; set; } = string.Empty;
  public string? ReferenceType { get; set; }
  public string? ReferenceId { get; set; }
  public int PeriodYear { get; set; }
  public int PeriodMonth { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}