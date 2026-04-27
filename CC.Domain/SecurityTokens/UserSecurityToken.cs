namespace CC.Domain.SecurityTokens;

public sealed class UserSecurityToken
{
  public Guid Id { get; set; }
  public Guid TenantId { get; set; }
  public Guid UserId { get; set; }
  public UserSecurityTokenPurpose Purpose { get; set; }
  public string TokenHash { get; set; } = string.Empty;
  public DateTime ExpiresAt { get; set; }
  public DateTime? UsedAt { get; set; }
  public DateTime? RevokedAt { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public Guid? CreatedByUserId { get; set; }
  public string? ConsumedIp { get; set; }
  public string? ConsumedUserAgent { get; set; }
}