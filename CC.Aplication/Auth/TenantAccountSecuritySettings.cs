using CC.Aplication.Notifications;
using Microsoft.Extensions.Options;

namespace CC.Aplication.Auth;

public sealed class TenantAccountSecuritySettings
{
  public TenantAccountSecuritySettings(
      IOptions<SecurityTokenOptions> securityTokenOptions,
      IOptions<EmailOptions> emailOptions)
  {
    SecurityTokens = securityTokenOptions.Value;
    Email = emailOptions.Value;
  }

  public SecurityTokenOptions SecurityTokens { get; }
  public EmailOptions Email { get; }
}