using Microsoft.Extensions.Logging;

namespace Api_eCommerce.Push
{
 public class WebPushSenderStub : IWebPushSender
 {
 private readonly ILogger<WebPushSenderStub> _logger;
 public WebPushSenderStub(ILogger<WebPushSenderStub> logger) { _logger = logger; }
 public Task<int> SendAsync(IEnumerable<(string endpoint,string p256dh,string auth)> subs, object payload, CancellationToken ct = default)
 {
 var count = subs.Count();
 _logger.LogInformation("Stub push send: {count} recipients", count);
 return Task.FromResult(count);
 }
 }
}