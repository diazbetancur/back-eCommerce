using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace CC.Aplication.Notifications;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
  private readonly IConfiguration _configuration;

  public EmailOptionsValidator(IConfiguration configuration)
  {
    _configuration = configuration;
  }

  public ValidateOptionsResult Validate(string? name, EmailOptions options)
  {
    var failures = new List<string>();
    var provider = (options.Provider ?? EmailOptions.NoOpProvider).Trim();

    ValidateProvider(provider, failures);
    ValidateCommonOptions(options, failures);

    if (provider.Equals(EmailOptions.ResendProvider, StringComparison.OrdinalIgnoreCase))
    {
      ValidateResendOptions(options, failures);
    }

    return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
  }

  private void ValidateResendOptions(EmailOptions options, List<string> failures)
  {
    if (!options.EnableEmailSending)
    {
      return;
    }

    ValidateResendApiKey(failures);
    ValidateResendSender(options, failures);
    ValidateSupportEmail(options, failures);
  }

  private void ValidateResendApiKey(List<string> failures)
  {
    if (string.IsNullOrWhiteSpace(_configuration["Resend:ApiKey"]))
    {
      failures.Add("Resend:ApiKey is required when Email:Provider=Resend.");
    }
  }

  private static void ValidateProvider(string provider, List<string> failures)
  {
    if (!provider.Equals(EmailOptions.NoOpProvider, StringComparison.OrdinalIgnoreCase)
        && !provider.Equals(EmailOptions.ResendProvider, StringComparison.OrdinalIgnoreCase))
    {
      failures.Add("Email:Provider must be either 'NoOp' or 'Resend'.");
    }
  }

  private static void ValidateCommonOptions(EmailOptions options, List<string> failures)
  {
    if (!IsValidPath(options.ActivationPath))
    {
      failures.Add("Email:ActivationPath must start with '/'.");
    }

    if (!IsValidPath(options.ResetPasswordPath))
    {
      failures.Add("Email:ResetPasswordPath must start with '/'.");
    }

    if (string.IsNullOrWhiteSpace(options.PublicBaseDomain))
    {
      failures.Add("Email:PublicBaseDomain is required.");
    }
  }

  private static void ValidateResendSender(EmailOptions options, List<string> failures)
  {
    if (string.IsNullOrWhiteSpace(options.FromName))
    {
      failures.Add("Email:FromName is required when Email:Provider=Resend.");
    }

    if (!TryGetEmailDomain(options.FromEmail, out var fromDomain))
    {
      failures.Add("Email:FromEmail must be a valid email address when Email:Provider=Resend.");
      return;
    }

    if (fromDomain.StartsWith("example.", StringComparison.OrdinalIgnoreCase))
    {
      failures.Add("Email:FromEmail cannot use a placeholder domain such as example.com.");
    }

    ValidateNotificationsDomain(options, fromDomain, failures);
  }

  private static void ValidateNotificationsDomain(EmailOptions options, string fromDomain, List<string> failures)
  {
    if (string.IsNullOrWhiteSpace(options.NotificationsDomain))
    {
      failures.Add("Email:NotificationsDomain is required when Email:Provider=Resend.");
      return;
    }

    if (!string.Equals(fromDomain, options.NotificationsDomain.Trim(), StringComparison.OrdinalIgnoreCase))
    {
      failures.Add("Email:FromEmail must belong to the configured Email:NotificationsDomain.");
    }
  }

  private static void ValidateSupportEmail(EmailOptions options, List<string> failures)
  {
    if (!IsValidEmail(options.SupportEmail))
    {
      failures.Add("Email:SupportEmail must be a valid email address when Email:Provider=Resend.");
    }
  }

  private static bool IsValidPath(string? value)
  {
    return !string.IsNullOrWhiteSpace(value) && value.StartsWith("/", StringComparison.Ordinal);
  }

  private static bool IsValidEmail(string? value)
  {
    return TryGetEmailDomain(value, out _);
  }

  private static bool TryGetEmailDomain(string? email, out string domain)
  {
    domain = string.Empty;

    if (string.IsNullOrWhiteSpace(email))
    {
      return false;
    }

    try
    {
      var address = new MailAddress(email.Trim());
      domain = address.Host.Trim().ToLowerInvariant();
      return true;
    }
    catch (FormatException)
    {
      return false;
    }
  }
}