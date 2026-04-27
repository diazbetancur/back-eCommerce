using CC.Domain.Interfaces.Notifications;
using CC.Domain.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CC.Infraestructure.Notifications;

public sealed class ResendEmailProvider : IEmailProvider
{
  private const string ProviderName = "Resend";

  private readonly HttpClient _httpClient;
  private readonly ResendOptions _options;
  private readonly ILogger<ResendEmailProvider> _logger;

  public ResendEmailProvider(
      HttpClient httpClient,
      IOptions<ResendOptions> options,
      ILogger<ResendEmailProvider> logger)
  {
    _httpClient = httpClient;
    _options = options.Value;
    _logger = logger;
  }

  public async Task<EmailSendResult> SendAsync(EmailSendRequest request, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(_options.ApiKey))
    {
      return Failed("CONFIGURATION_ERROR", "Resend API key is not configured.");
    }

    if (string.IsNullOrWhiteSpace(request.Recipient)
        || string.IsNullOrWhiteSpace(request.Subject)
        || string.IsNullOrWhiteSpace(request.FromEmail)
        || (string.IsNullOrWhiteSpace(request.HtmlBody) && string.IsNullOrWhiteSpace(request.TextBody)))
    {
      return Failed("INVALID_REQUEST", "Email request is incomplete.");
    }

    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "emails");
    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    httpRequest.Content = JsonContent.Create(BuildPayload(request));

    try
    {
      using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
      if (response.IsSuccessStatusCode)
      {
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
        var providerMessageId = TryReadString(payload?.RootElement, "id");
        _logger.LogInformation(
            "Resend accepted email for {Recipient}. ProviderMessageId {ProviderMessageId}",
            MaskEmail(request.Recipient),
            providerMessageId);

        return new EmailSendResult
        {
          Accepted = true,
          Provider = ProviderName,
          ProviderMessageId = providerMessageId
        };
      }

      var error = await ReadErrorAsync(response, ct);
      _logger.LogWarning(
          "Resend rejected email for {Recipient}. StatusCode {StatusCode}, ErrorCode {ErrorCode}",
          MaskEmail(request.Recipient),
          (int)response.StatusCode,
          error.ErrorCode);

      return new EmailSendResult
      {
        Accepted = false,
        Provider = ProviderName,
        ErrorCode = error.ErrorCode,
        ErrorMessage = error.ErrorMessage
      };
    }
    catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
    {
      _logger.LogWarning(ex, "Resend request timed out for {Recipient}", MaskEmail(request.Recipient));
      return Failed("TIMEOUT", "The email provider request timed out.");
    }
  }

  private static object BuildPayload(EmailSendRequest request)
  {
    var payload = new Dictionary<string, object?>
    {
      ["from"] = FormatFrom(request.FromEmail!, request.FromName),
      ["to"] = new[] { request.Recipient },
      ["subject"] = request.Subject,
      ["html"] = request.HtmlBody
    };

    if (!string.IsNullOrWhiteSpace(request.TextBody))
    {
      payload["text"] = request.TextBody;
    }

    if (!string.IsNullOrWhiteSpace(request.ReplyTo))
    {
      payload["reply_to"] = request.ReplyTo;
    }

    return payload;
  }

  private static string FormatFrom(string fromEmail, string? fromName)
  {
    return string.IsNullOrWhiteSpace(fromName)
        ? fromEmail.Trim()
        : $"{fromName.Trim()} <{fromEmail.Trim()}>";
  }

  private static EmailSendResult Failed(string errorCode, string errorMessage)
  {
    return new EmailSendResult
    {
      Accepted = false,
      Provider = ProviderName,
      ErrorCode = errorCode,
      ErrorMessage = errorMessage
    };
  }

  private static async Task<(string ErrorCode, string ErrorMessage)> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
  {
    var fallbackCode = $"HTTP_{(int)response.StatusCode}";
    var fallbackMessage = $"Resend rejected the email request with status code {(int)response.StatusCode}.";

    if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true)
    {
      return (fallbackCode, fallbackMessage);
    }

    try
    {
      var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
      if (payload == null)
      {
        return (fallbackCode, fallbackMessage);
      }

      return (
          string.IsNullOrWhiteSpace(TryReadString(payload.RootElement, "name")) ? fallbackCode : TryReadString(payload.RootElement, "name")!,
          string.IsNullOrWhiteSpace(TryReadString(payload.RootElement, "message")) ? fallbackMessage : Truncate(TryReadString(payload.RootElement, "message")!, 300));
    }
    catch (JsonException)
    {
      return (fallbackCode, fallbackMessage);
    }
  }

  private static string? TryReadString(JsonElement? rootElement, string propertyName)
  {
    if (rootElement == null || rootElement.Value.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    if (!rootElement.Value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
    {
      return null;
    }

    return property.GetString();
  }

  private static string MaskEmail(string email)
  {
    var parts = email.Split('@', 2, StringSplitOptions.TrimEntries);
    if (parts.Length != 2 || parts[0].Length == 0)
    {
      return "***";
    }

    var local = parts[0];
    var maskedLocal = local.Length == 1 ? "*" : $"{local[0]}***{local[^1]}";
    return $"{maskedLocal}@{parts[1]}";
  }

  private static string Truncate(string value, int maxLength)
  {
    return value.Length <= maxLength ? value : value[..maxLength];
  }
}