namespace CC.Domain.Notifications;

public sealed class EmailSendRequest
{
  public string Recipient { get; init; } = string.Empty;
  public string? Subject { get; init; }
  public string? HtmlBody { get; init; }
  public string? TextBody { get; init; }
  public string? FromEmail { get; init; }
  public string? FromName { get; init; }
  public string? ReplyTo { get; init; }
}