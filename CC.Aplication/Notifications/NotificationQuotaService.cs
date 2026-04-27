using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;

namespace CC.Aplication.Notifications;

public sealed class NotificationQuotaService : INotificationQuotaService
{
  private readonly INotificationTenantPlanRepository _tenantPlans;
  private readonly ITenantNotificationQuotaRepository _quotas;
  private readonly ITenantNotificationCreditLedgerRepository _ledger;
  private readonly INotificationUnitOfWork _unitOfWork;

  public NotificationQuotaService(
      INotificationTenantPlanRepository tenantPlans,
      ITenantNotificationQuotaRepository quotas,
      ITenantNotificationCreditLedgerRepository ledger,
      INotificationUnitOfWork unitOfWork)
  {
    _tenantPlans = tenantPlans;
    _quotas = quotas;
    _ledger = ledger;
    _unitOfWork = unitOfWork;
  }

  public async Task<TenantNotificationQuota> GetOrCreateCurrentMonthlyQuotaAsync(Guid tenantId, CancellationToken ct = default)
  {
    var now = DateTime.UtcNow;
    var quota = await _quotas.GetByTenantAndPeriodAsync(tenantId, now.Year, now.Month, ct);
    if (quota != null)
    {
      return quota;
    }

    quota = new TenantNotificationQuota
    {
      TenantId = tenantId,
      PeriodYear = now.Year,
      PeriodMonth = now.Month,
      CreatedAt = now
    };

    _quotas.Add(quota);
    await _unitOfWork.SaveChangesAsync(ct);
    return quota;
  }

  public async Task<TenantNotificationQuota> GrantMonthlyPlanCreditsAsync(Guid tenantId, CancellationToken ct = default)
  {
    var quota = await GetOrCreateCurrentMonthlyQuotaAsync(tenantId, ct);
    var alreadyGranted = await _ledger.ExistsMonthlyGrantAsync(tenantId, NotificationChannel.Email, quota.PeriodYear, quota.PeriodMonth, ct);
    if (alreadyGranted)
    {
      return quota;
    }

    quota.IncludedEmailCredits = Math.Max(0, await _tenantPlans.GetIncludedEmailCreditsAsync(tenantId, ct));
    quota.UpdatedAt = DateTime.UtcNow;
    _quotas.Update(quota);

    _ledger.Add(new TenantNotificationCreditLedger
    {
      TenantId = tenantId,
      Channel = NotificationChannel.Email,
      MovementType = NotificationCreditMovementType.MonthlyPlanGrant,
      Quantity = quota.IncludedEmailCredits,
      Reason = "Monthly plan email credits grant",
      ReferenceType = "PlanLimit",
      ReferenceId = NotificationPlanLimitCodes.IncludedEmailCreditsPerMonth,
      PeriodYear = quota.PeriodYear,
      PeriodMonth = quota.PeriodMonth,
      CreatedAt = DateTime.UtcNow
    });

    await _unitOfWork.SaveChangesAsync(ct);
    return quota;
  }

  public async Task<TenantNotificationQuota> AddPurchasedCreditsForCurrentMonthAsync(Guid tenantId, int quantity, string reason, string? referenceType = null, string? referenceId = null, CancellationToken ct = default)
  {
    if (quantity <= 0)
    {
      throw new ArgumentOutOfRangeException(nameof(quantity), "Purchased credits must be greater than zero.");
    }

    var quota = await GrantMonthlyPlanCreditsAsync(tenantId, ct);
    quota.PurchasedEmailCredits += quantity;
    quota.UpdatedAt = DateTime.UtcNow;
    _quotas.Update(quota);

    _ledger.Add(new TenantNotificationCreditLedger
    {
      TenantId = tenantId,
      Channel = NotificationChannel.Email,
      MovementType = NotificationCreditMovementType.PackagePurchase,
      Quantity = quantity,
      Reason = reason,
      ReferenceType = referenceType,
      ReferenceId = referenceId,
      PeriodYear = quota.PeriodYear,
      PeriodMonth = quota.PeriodMonth,
      CreatedAt = DateTime.UtcNow
    });

    await _unitOfWork.SaveChangesAsync(ct);
    return quota;
  }

  public async Task<bool> TryConsumeEmailCreditAsync(Guid tenantId, string reason, string? referenceType = null, string? referenceId = null, CancellationToken ct = default)
  {
    var quota = await GrantMonthlyPlanCreditsAsync(tenantId, ct);
    var consumed = await _quotas.TryConsumeEmailCreditAsync(tenantId, quota.PeriodYear, quota.PeriodMonth, ct);
    if (!consumed)
    {
      return false;
    }

    _ledger.Add(new TenantNotificationCreditLedger
    {
      TenantId = tenantId,
      Channel = NotificationChannel.Email,
      MovementType = NotificationCreditMovementType.EmailSent,
      Quantity = -1,
      Reason = reason,
      ReferenceType = referenceType,
      ReferenceId = referenceId,
      PeriodYear = quota.PeriodYear,
      PeriodMonth = quota.PeriodMonth,
      CreatedAt = DateTime.UtcNow
    });

    await _unitOfWork.SaveChangesAsync(ct);
    return true;
  }

  public async Task<int> GetAvailableCreditsAsync(Guid tenantId, CancellationToken ct = default)
  {
    var quota = await GrantMonthlyPlanCreditsAsync(tenantId, ct);
    return quota.GetAvailableCredits();
  }
}