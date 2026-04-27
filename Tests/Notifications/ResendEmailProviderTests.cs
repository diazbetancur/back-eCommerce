using CC.Aplication.Notifications;
using CC.Domain.Notifications;
using CC.Infraestructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Tests.Notifications;

public class ResendEmailProviderTests
{
  [Fact]
  public async Task SendAsync_WhenResendAccepts_ReturnsAcceptedResult()
  {
    var handler = new StubHttpMessageHandler((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("{\"id\":\"re_123\"}", Encoding.UTF8, "application/json")
        }));

    var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.resend.com/")
    };

    var provider = new ResendEmailProvider(
        httpClient,
        Options.Create(new ResendOptions { ApiKey = "re_test_key" }),
        NullLogger<ResendEmailProvider>.Instance);

    var result = await provider.SendAsync(new EmailSendRequest
    {
      Recipient = "john@example.com",
      Subject = "Hello",
      HtmlBody = "<p>Hello</p>",
      TextBody = "Hello",
      FromEmail = "no-reply@notifications.tuecom.online",
      FromName = "TueCom",
      ReplyTo = "support@tuecom.online"
    });

    result.Accepted.Should().BeTrue();
    result.Provider.Should().Be(EmailOptions.ResendProvider);
    result.ProviderMessageId.Should().Be("re_123");
    handler.LastAuthorization.Should().NotBeNull();
    handler.LastAuthorization!.Scheme.Should().Be("Bearer");
    handler.LastAuthorization.Parameter.Should().Be("re_test_key");

    using var requestPayload = JsonDocument.Parse(handler.LastBody!);
    requestPayload.RootElement.GetProperty("from").GetString().Should().Be("TueCom <no-reply@notifications.tuecom.online>");
    requestPayload.RootElement.GetProperty("reply_to").GetString().Should().Be("support@tuecom.online");
  }

  [Fact]
  public async Task SendAsync_WhenResendRejects_ReturnsFailedResult()
  {
    var handler = new StubHttpMessageHandler((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
          Content = new StringContent("{\"name\":\"invalid_from\",\"message\":\"Sender domain not verified.\"}", Encoding.UTF8, "application/json")
        }));

    var httpClient = new HttpClient(handler)
    {
      BaseAddress = new Uri("https://api.resend.com/")
    };

    var provider = new ResendEmailProvider(
        httpClient,
        Options.Create(new ResendOptions { ApiKey = "re_test_key" }),
        NullLogger<ResendEmailProvider>.Instance);

    var result = await provider.SendAsync(new EmailSendRequest
    {
      Recipient = "john@example.com",
      Subject = "Hello",
      HtmlBody = "<p>Hello</p>",
      FromEmail = "no-reply@notifications.tuecom.online"
    });

    result.Accepted.Should().BeFalse();
    result.Provider.Should().Be(EmailOptions.ResendProvider);
    result.ErrorCode.Should().Be("invalid_from");
    result.ErrorMessage.Should().Be("Sender domain not verified.");
  }

  private sealed class StubHttpMessageHandler : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
      _responseFactory = responseFactory;
    }

    public AuthenticationHeaderValue? LastAuthorization { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      LastAuthorization = request.Headers.Authorization;
      LastBody = request.Content == null
          ? null
          : await request.Content.ReadAsStringAsync(cancellationToken);

      return await _responseFactory(request, cancellationToken);
    }
  }
}