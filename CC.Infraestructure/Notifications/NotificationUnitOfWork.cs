using CC.Domain.Interfaces.Notifications;
using CC.Infraestructure.AdminDb;

namespace CC.Infraestructure.Notifications;

public sealed class NotificationUnitOfWork : INotificationUnitOfWork
{
  private readonly AdminDbContext _adminDb;

  public NotificationUnitOfWork(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public Task<int> SaveChangesAsync(CancellationToken ct = default)
  {
    return _adminDb.SaveChangesAsync(ct);
  }
}