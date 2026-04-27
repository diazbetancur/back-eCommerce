namespace CC.Aplication.Auth;

public sealed class SecurityTokenOptions
{
  public const string SectionName = "SecurityTokens";

  public int ActivationTokenExpirationHours { get; init; } = 48;
  public int PasswordResetTokenExpirationHours { get; init; } = 2;
  public int RetentionDays { get; init; } = 10;
}