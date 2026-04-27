using CC.Domain.Notifications;

namespace CC.Domain.Interfaces.Notifications;

public interface INotificationTemplateRepository
{
  Task<NotificationTemplate?> GetActiveByCodeAsync(string code, NotificationChannel channel, CancellationToken ct = default);
}