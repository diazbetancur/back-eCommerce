namespace CC.Domain.Interfaces.Notifications;

public interface INotificationUnitOfWork
{
  Task<int> SaveChangesAsync(CancellationToken ct = default);
}