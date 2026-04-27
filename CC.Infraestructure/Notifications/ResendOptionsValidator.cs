using Microsoft.Extensions.Options;

namespace CC.Infraestructure.Notifications;

public sealed class ResendOptionsValidator : IValidateOptions<ResendOptions>
{
  public ValidateOptionsResult Validate(string? name, ResendOptions options)
  {
    var failures = new List<string>();

    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
        || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
    {
      failures.Add("Resend:BaseUrl must be a valid absolute HTTP or HTTPS URL.");
    }

    if (options.TimeoutSeconds <= 0 || options.TimeoutSeconds > 120)
    {
      failures.Add("Resend:TimeoutSeconds must be between 1 and 120.");
    }

    return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
  }
}