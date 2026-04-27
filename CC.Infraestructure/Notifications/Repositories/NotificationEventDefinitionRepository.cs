using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Notifications.Repositories;

public sealed class NotificationEventDefinitionRepository : INotificationEventDefinitionRepository
{
  private readonly AdminDbContext _adminDb;

  public NotificationEventDefinitionRepository(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public Task<NotificationEventDefinition?> GetByCodeAsync(string code, NotificationChannel channel, CancellationToken ct = default)
  {
    return _adminDb.NotificationEventDefinitions
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Code == code && item.Channel == channel, ct);
  }

  public Task<List<NotificationEventDefinition>> GetActiveByChannelAsync(NotificationChannel channel, CancellationToken ct = default)
  {
    return _adminDb.NotificationEventDefinitions
        .AsNoTracking()
        .Where(item => item.Channel == channel && item.IsActive)
        .OrderBy(item => item.Code)
        .ToListAsync(ct);
  }
}