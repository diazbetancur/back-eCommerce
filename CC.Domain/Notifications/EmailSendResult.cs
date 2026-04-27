namespace CC.Domain.Notifications;

public sealed class EmailSendResult
{
    public bool Accepted { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string? ProviderMessageId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}