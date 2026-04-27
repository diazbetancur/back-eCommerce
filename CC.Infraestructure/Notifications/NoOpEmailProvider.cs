using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;

namespace CC.Infraestructure.Notifications;

public sealed class NoOpEmailProvider : IEmailProvider
{
  public Task<EmailSendResult> SendAsync(EmailSendRequest request, CancellationToken ct = default)
  {
    return Task.FromResult(new EmailSendResult
    {
      Accepted = true,
      Provider = "NoOp",
      ProviderMessageId = Guid.NewGuid().ToString("N")
    });
  }
}