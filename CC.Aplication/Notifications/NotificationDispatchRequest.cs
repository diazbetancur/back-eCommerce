using CC.Domain.Notifications;

namespace CC.Aplication.Notifications;

public sealed class NotificationDispatchRequest
{
  public Guid? TenantId { get; init; }
  public string EventCode { get; init; } = string.Empty;
  public NotificationChannel Channel { get; init; }
  public string Recipient { get; init; } = string.Empty;
  public string? FromEmail { get; init; }
  public string? FromName { get; init; }
  public string? ReplyTo { get; init; }
  public IReadOnlyDictionary<string, string?> Variables { get; init; } = new Dictionary<string, string?>();
  public string? ReferenceType { get; init; }
  public string? ReferenceId { get; init; }
}