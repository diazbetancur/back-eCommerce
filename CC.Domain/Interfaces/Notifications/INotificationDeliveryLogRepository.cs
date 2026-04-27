using CC.Domain.Notifications;

namespace CC.Domain.Interfaces.Notifications;

public interface INotificationDeliveryLogRepository
{
  void Add(NotificationDeliveryLog log);
}