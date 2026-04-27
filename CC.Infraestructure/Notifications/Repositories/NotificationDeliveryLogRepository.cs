using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;

namespace CC.Infraestructure.Notifications.Repositories;

public sealed class NotificationDeliveryLogRepository : INotificationDeliveryLogRepository
{
  private readonly AdminDbContext _adminDb;

  public NotificationDeliveryLogRepository(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public void Add(NotificationDeliveryLog log)
  {
    _adminDb.NotificationDeliveryLogs.Add(log);
  }
}