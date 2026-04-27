using CC.Domain.Notifications;

namespace CC.Domain.Interfaces.Notifications;

public interface INotificationEventDefinitionRepository
{
  Task<NotificationEventDefinition?> GetByCodeAsync(string code, NotificationChannel channel, CancellationToken ct = default);
  Task<List<NotificationEventDefinition>> GetActiveByChannelAsync(NotificationChannel channel, CancellationToken ct = default);
}