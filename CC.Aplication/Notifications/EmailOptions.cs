namespace CC.Aplication.Notifications;

public sealed class EmailOptions
{
  public const string SectionName = "Email";
  public const string NoOpProvider = "NoOp";
  public const string ResendProvider = "Resend";

  public string Provider { get; set; } = NoOpProvider;
  public string FromEmail { get; set; } = string.Empty;
  public string FromName { get; set; } = "TueCom";
  public string SupportEmail { get; set; } = "support@tuecom.online";
  public string PublicBaseDomain { get; set; } = "tuecom.online";
  public string NotificationsDomain { get; set; } = "notifications.tuecom.online";
  public string ActivationPath { get; set; } = "/activate-account";
  public string ResetPasswordPath { get; set; } = "/reset-password";
  public bool EnableEmailSending { get; set; }
  public bool FailOnProviderError { get; set; }
}