using CC.Domain.Notifications;

namespace CC.Domain.Interfaces.Notifications;

public interface IEmailProvider
{
  Task<EmailSendResult> SendAsync(EmailSendRequest request, CancellationToken ct = default);
}