namespace CC.Infraestructure.Notifications;

public sealed class ResendOptions
{
  public const string SectionName = "Resend";

  public string ApiKey { get; set; } = string.Empty;
  public string BaseUrl { get; set; } = "https://api.resend.com/";
  public int TimeoutSeconds { get; set; } = 15;
}