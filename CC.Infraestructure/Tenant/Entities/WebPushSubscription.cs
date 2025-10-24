namespace CC.Infraestructure.Tenant.Entities
{
 public class WebPushSubscription
 {
 public Guid Id { get; set; }
 public Guid? UserId { get; set; }
 public string Endpoint { get; set; }
 public string P256dh { get; set; }
 public string Auth { get; set; }
 public string UserAgent { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 }
}