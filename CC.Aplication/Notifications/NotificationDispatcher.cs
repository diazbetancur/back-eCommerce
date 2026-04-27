using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CC.Aplication.Notifications;

public sealed class NotificationDispatcher : INotificationDispatcher
{
  private readonly INotificationEligibilityService _eligibilityService;
  private readonly INotificationQuotaService _quotaService;
  private readonly INotificationDeliveryLogRepository _deliveryLogs;
  private readonly INotificationUnitOfWork _unitOfWork;
  private readonly IEmailProvider _emailProvider;
  private readonly EmailOptions _emailOptions;
  private readonly ILogger<NotificationDispatcher> _logger;

  public NotificationDispatcher(
      INotificationEligibilityService eligibilityService,
      INotificationQuotaService quotaService,
      INotificationDeliveryLogRepository deliveryLogs,
      INotificationUnitOfWork unitOfWork,
      IEmailProvider emailProvider,
      IOptions<EmailOptions> emailOptions,
      ILogger<NotificationDispatcher> logger)
  {
    _eligibilityService = eligibilityService;
    _quotaService = quotaService;
    _deliveryLogs = deliveryLogs;
    _unitOfWork = unitOfWork;
    _emailProvider = emailProvider;
    _emailOptions = emailOptions.Value;
    _logger = logger;
  }

  public async Task<NotificationDispatchResult> DispatchAsync(NotificationDispatchRequest request, CancellationToken ct = default)
  {
    var eligibility = await _eligibilityService.EvaluateAsync(request.TenantId, request.EventCode, request.Channel, request.Recipient, ct);
    if (!eligibility.CanSend)
    {
      var skippedLog = CreateLog(request, eligibility, null, null, 0);
      _deliveryLogs.Add(skippedLog);
      await _unitOfWork.SaveChangesAsync(ct);

      return new NotificationDispatchResult
      {
        Accepted = false,
        Status = skippedLog.Status,
        DeliveryLogId = skippedLog.Id,
        Message = eligibility.FailureMessage
      };
    }

    if (request.Channel != NotificationChannel.Email)
    {
      var unsupportedEligibility = new NotificationEligibilityResult
      {
        CanSend = false,
        SkipReason = NotificationDeliveryStatus.SkippedSystemRule,
        EventDefinition = eligibility.EventDefinition,
        Template = eligibility.Template,
        FailureMessage = "The requested notification channel is not implemented in phase 1."
      };

      var unsupportedLog = CreateLog(request, unsupportedEligibility, null, null, 0);
      _deliveryLogs.Add(unsupportedLog);
      await _unitOfWork.SaveChangesAsync(ct);

      return new NotificationDispatchResult
      {
        Accepted = false,
        Status = unsupportedLog.Status,
        DeliveryLogId = unsupportedLog.Id,
        Message = unsupportedEligibility.FailureMessage
      };
    }

    var emailRequest = BuildEmailRequest(request, eligibility);
    var providerResult = await _emailProvider.SendAsync(emailRequest, ct);

    if (!providerResult.Accepted)
    {
      var failedLog = CreateLog(request, eligibility, emailRequest, providerResult, 0);
      failedLog.Status = NotificationDeliveryStatus.Failed;
      failedLog.SentAt = null;
      _deliveryLogs.Add(failedLog);
      await _unitOfWork.SaveChangesAsync(ct);

      _logger.LogWarning(
          "Notification dispatch failed. Event {EventCode}, Provider {Provider}, Recipient {Recipient}, Status {Status}, ErrorCode {ErrorCode}",
          request.EventCode,
          providerResult.Provider,
          MaskRecipient(request.Recipient),
          failedLog.Status,
          providerResult.ErrorCode);

      if (_emailOptions.FailOnProviderError)
      {
        throw new InvalidOperationException($"Email provider '{providerResult.Provider}' rejected notification '{request.EventCode}'.");
      }

      return new NotificationDispatchResult
      {
        Accepted = false,
        Status = failedLog.Status,
        DeliveryLogId = failedLog.Id,
        Message = providerResult.ErrorMessage
      };
    }

    var consumedCredits = 0;
    if (eligibility.ConsumesQuota && request.TenantId.HasValue && ShouldConsumeQuota(providerResult))
    {
      var consumed = await _quotaService.TryConsumeEmailCreditAsync(
          request.TenantId.Value,
          $"Notification sent for event {request.EventCode}",
          request.ReferenceType,
          request.ReferenceId,
          ct);

      if (!consumed)
      {
        var quotaEligibility = new NotificationEligibilityResult
        {
          CanSend = false,
          SkipReason = NotificationDeliveryStatus.SkippedQuotaExceeded,
          EventDefinition = eligibility.EventDefinition,
          Template = eligibility.Template,
          FailureMessage = "Notification quota exceeded during consumption."
        };

        var quotaLog = CreateLog(request, quotaEligibility, emailRequest, providerResult, 0);
        _deliveryLogs.Add(quotaLog);
        await _unitOfWork.SaveChangesAsync(ct);

        return new NotificationDispatchResult
        {
          Accepted = false,
          Status = quotaLog.Status,
          DeliveryLogId = quotaLog.Id,
          Message = quotaEligibility.FailureMessage
        };
      }

      consumedCredits = 1;
    }

    var acceptedLog = CreateLog(request, eligibility, emailRequest, providerResult, consumedCredits);
    acceptedLog.Status = ResolveAcceptedStatus(providerResult);
    acceptedLog.SentAt = acceptedLog.Status == NotificationDeliveryStatus.Sent ? DateTime.UtcNow : null;
    _deliveryLogs.Add(acceptedLog);
    await _unitOfWork.SaveChangesAsync(ct);

    _logger.LogInformation(
        "Notification dispatch completed. Event {EventCode}, Provider {Provider}, Recipient {Recipient}, Status {Status}, ProviderMessageId {ProviderMessageId}",
        request.EventCode,
        providerResult.Provider,
        MaskRecipient(request.Recipient),
        acceptedLog.Status,
        providerResult.ProviderMessageId);

    return new NotificationDispatchResult
    {
      Accepted = true,
      Status = acceptedLog.Status,
      DeliveryLogId = acceptedLog.Id,
      ConsumedQuota = consumedCredits > 0,
      ProviderMessageId = providerResult.ProviderMessageId,
      Message = BuildAcceptedMessage(acceptedLog.Status, providerResult.Provider)
    };
  }

  private EmailSendRequest BuildEmailRequest(NotificationDispatchRequest request, NotificationEligibilityResult eligibility)
  {
    return new EmailSendRequest
    {
      Recipient = request.Recipient,
      Subject = Render(eligibility.Template?.SubjectTemplate, request.Variables),
      HtmlBody = Render(eligibility.Template?.HtmlTemplate, request.Variables),
      TextBody = Render(eligibility.Template?.TextTemplate, request.Variables),
      FromEmail = request.FromEmail ?? _emailOptions.FromEmail,
      FromName = request.FromName ?? _emailOptions.FromName,
      ReplyTo = request.ReplyTo ?? _emailOptions.SupportEmail
    };
  }

  private static NotificationDeliveryLog CreateLog(
      NotificationDispatchRequest request,
      NotificationEligibilityResult eligibility,
      EmailSendRequest? emailRequest,
      EmailSendResult? providerResult,
      int consumedCredits)
  {
    return new NotificationDeliveryLog
    {
      TenantId = request.TenantId,
      EventCode = request.EventCode,
      TemplateCode = eligibility.Template?.Code ?? eligibility.EventDefinition?.TemplateCode,
      Channel = request.Channel,
      Recipient = request.Recipient,
      FromEmail = emailRequest?.FromEmail,
      FromName = emailRequest?.FromName,
      ReplyTo = emailRequest?.ReplyTo,
      Subject = emailRequest?.Subject ?? Render(eligibility.Template?.SubjectTemplate, request.Variables),
      Status = ResolveStatus(eligibility, providerResult),
      Provider = providerResult?.Provider,
      ProviderMessageId = providerResult?.ProviderMessageId,
      ErrorCode = providerResult?.ErrorCode,
      ErrorMessage = eligibility.CanSend ? providerResult?.ErrorMessage : eligibility.FailureMessage,
      ConsumedCredits = consumedCredits,
      ReferenceType = request.ReferenceType,
      ReferenceId = request.ReferenceId,
      CreatedAt = DateTime.UtcNow,
      SentAt = ResolveStatus(eligibility, providerResult) == NotificationDeliveryStatus.Sent ? DateTime.UtcNow : null
    };
  }

  private static NotificationDeliveryStatus ResolveStatus(NotificationEligibilityResult eligibility, EmailSendResult? providerResult)
  {
    if (!eligibility.CanSend)
    {
      return eligibility.SkipReason ?? NotificationDeliveryStatus.SkippedSystemRule;
    }

    if (providerResult == null)
    {
      return NotificationDeliveryStatus.Pending;
    }

    return providerResult.Accepted ? ResolveAcceptedStatus(providerResult) : NotificationDeliveryStatus.Failed;
  }

  private static NotificationDeliveryStatus ResolveAcceptedStatus(EmailSendResult providerResult)
  {
    return IsNoOpProvider(providerResult) ? NotificationDeliveryStatus.Pending : NotificationDeliveryStatus.Sent;
  }

  private static bool ShouldConsumeQuota(EmailSendResult providerResult)
  {
    return providerResult.Accepted && !IsNoOpProvider(providerResult);
  }

  private static bool IsNoOpProvider(EmailSendResult providerResult)
  {
    return string.Equals(providerResult.Provider, EmailOptions.NoOpProvider, StringComparison.OrdinalIgnoreCase);
  }

  private static string BuildAcceptedMessage(NotificationDeliveryStatus status, string provider)
  {
    return status == NotificationDeliveryStatus.Pending && string.Equals(provider, EmailOptions.NoOpProvider, StringComparison.OrdinalIgnoreCase)
        ? "Notification recorded by NoOp provider. No real email was sent."
        : "Notification sent successfully.";
  }

  private static string MaskRecipient(string recipient)
  {
    var parts = recipient.Split('@', 2, StringSplitOptions.TrimEntries);
    if (parts.Length != 2 || parts[0].Length == 0)
    {
      return "***";
    }

    var local = parts[0];
    var maskedLocal = local.Length == 1 ? "*" : $"{local[0]}***{local[^1]}";
    return $"{maskedLocal}@{parts[1]}";
  }

  private static string? Render(string? template, IReadOnlyDictionary<string, string?> variables)
  {
    if (string.IsNullOrWhiteSpace(template))
    {
      return template;
    }

    var rendered = template;
    foreach (var variable in variables)
    {
      rendered = rendered.Replace($"{{{{{variable.Key}}}}}", variable.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    return rendered;
  }
}