using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Notifications.Repositories;

public sealed class NotificationTemplateRepository : INotificationTemplateRepository
{
  private readonly AdminDbContext _adminDb;

  public NotificationTemplateRepository(AdminDbContext adminDb)
  {
    _adminDb = adminDb;
  }

  public Task<NotificationTemplate?> GetActiveByCodeAsync(string code, NotificationChannel channel, CancellationToken ct = default)
  {
    return _adminDb.NotificationTemplates
        .AsNoTracking()
        .Where(item => item.Code == code && item.Channel == channel && item.IsActive)
        .OrderByDescending(item => item.Version)
        .FirstOrDefaultAsync(ct);
  }
}