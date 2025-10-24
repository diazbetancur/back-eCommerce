namespace Api_eCommerce.Push
{
 public interface IWebPushSender
 {
 Task<int> SendAsync(IEnumerable<(string endpoint,string p256dh,string auth)> subs, object payload, CancellationToken ct = default);
 }
}