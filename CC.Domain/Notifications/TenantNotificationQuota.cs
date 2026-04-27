namespace CC.Domain.Notifications;

public class TenantNotificationQuota
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid TenantId { get; set; }
  public int PeriodYear { get; set; }
  public int PeriodMonth { get; set; }
  public int IncludedEmailCredits { get; set; }
  public int PurchasedEmailCredits { get; set; }
  public int UsedEmailCredits { get; set; }
  public int ReservedEmailCredits { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime? UpdatedAt { get; set; }

  public int GetAvailableCredits()
  {
    return IncludedEmailCredits + PurchasedEmailCredits - UsedEmailCredits - ReservedEmailCredits;
  }
}