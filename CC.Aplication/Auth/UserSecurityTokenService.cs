using CC.Domain.SecurityTokens;
using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace CC.Aplication.Auth;

public interface IUserSecurityTokenService
{
  Task<GeneratedUserSecurityToken> GenerateTenantAdminActivationTokenAsync(Guid tenantId, Guid userId, Guid? createdByUserId = null, CancellationToken ct = default);
  Task<GeneratedUserSecurityToken> GeneratePasswordResetTokenAsync(Guid tenantId, Guid userId, Guid? createdByUserId = null, CancellationToken ct = default);
  Task<UserSecurityTokenValidationResult> ValidateTokenAsync(string rawToken, UserSecurityTokenPurpose purpose, CancellationToken ct = default);
  Task ConsumeTokenAsync(Guid tokenId, string? ipAddress, string? userAgent, CancellationToken ct = default);
  Task RevokeActiveTokensAsync(Guid userId, UserSecurityTokenPurpose purpose, CancellationToken ct = default);
  Task CleanupOldTokensAsync(CancellationToken ct = default);
}

public sealed class UserSecurityTokenService : IUserSecurityTokenService
{
  private readonly AdminDbContext _adminDb;
  private readonly SecurityTokenOptions _options;
  private readonly ILogger<UserSecurityTokenService> _logger;

  public UserSecurityTokenService(
      AdminDbContext adminDb,
      IOptions<SecurityTokenOptions> options,
      ILogger<UserSecurityTokenService> logger)
  {
    _adminDb = adminDb;
    _options = options.Value;
    _logger = logger;
  }

  public Task<GeneratedUserSecurityToken> GenerateTenantAdminActivationTokenAsync(Guid tenantId, Guid userId, Guid? createdByUserId = null, CancellationToken ct = default)
  {
    return GenerateTokenAsync(
        tenantId,
        userId,
        UserSecurityTokenPurpose.TenantAdminActivation,
        _options.ActivationTokenExpirationHours,
        createdByUserId,
        ct);
  }

  public Task<GeneratedUserSecurityToken> GeneratePasswordResetTokenAsync(Guid tenantId, Guid userId, Guid? createdByUserId = null, CancellationToken ct = default)
  {
    return GenerateTokenAsync(
        tenantId,
        userId,
        UserSecurityTokenPurpose.PasswordReset,
        _options.PasswordResetTokenExpirationHours,
        createdByUserId,
        ct);
  }

  public async Task<UserSecurityTokenValidationResult> ValidateTokenAsync(string rawToken, UserSecurityTokenPurpose purpose, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(rawToken))
    {
      return UserSecurityTokenValidationResult.Fail(GetErrorCode(purpose, TokenValidationFailure.Invalid), "Security token is required.");
    }

    var tokenHash = ComputeTokenHash(rawToken);
    var token = await _adminDb.UserSecurityTokens
        .AsTracking()
        .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, ct);

    if (token == null || token.Purpose != purpose)
    {
      return UserSecurityTokenValidationResult.Fail(GetErrorCode(purpose, TokenValidationFailure.Invalid), "Security token is invalid.");
    }

    if (token.UsedAt.HasValue)
    {
      return UserSecurityTokenValidationResult.Fail(GetErrorCode(purpose, TokenValidationFailure.Used), "Security token was already used.");
    }

    if (token.RevokedAt.HasValue)
    {
      return UserSecurityTokenValidationResult.Fail(GetErrorCode(purpose, TokenValidationFailure.Revoked), "Security token was revoked.");
    }

    if (token.ExpiresAt <= DateTime.UtcNow)
    {
      return UserSecurityTokenValidationResult.Fail(GetErrorCode(purpose, TokenValidationFailure.Expired), "Security token expired.");
    }

    return UserSecurityTokenValidationResult.Success(token);
  }

  public async Task ConsumeTokenAsync(Guid tokenId, string? ipAddress, string? userAgent, CancellationToken ct = default)
  {
    var token = await _adminDb.UserSecurityTokens.FirstOrDefaultAsync(item => item.Id == tokenId, ct);
    if (token == null)
    {
      throw new InvalidOperationException($"User security token '{tokenId}' was not found.");
    }

    token.UsedAt = DateTime.UtcNow;
    token.ConsumedIp = TrimOrNull(ipAddress, 45);
    token.ConsumedUserAgent = TrimOrNull(userAgent, 1024);

    await _adminDb.SaveChangesAsync(ct);
  }

  public async Task RevokeActiveTokensAsync(Guid userId, UserSecurityTokenPurpose purpose, CancellationToken ct = default)
  {
    var now = DateTime.UtcNow;

    var activeTokens = await _adminDb.UserSecurityTokens
        .Where(item => item.UserId == userId
            && item.Purpose == purpose
            && item.UsedAt == null
            && item.RevokedAt == null
            && item.ExpiresAt > now)
        .ToListAsync(ct);

    if (activeTokens.Count == 0)
    {
      return;
    }

    foreach (var token in activeTokens)
    {
      token.RevokedAt = now;
    }

    await _adminDb.SaveChangesAsync(ct);
  }

  public async Task CleanupOldTokensAsync(CancellationToken ct = default)
  {
    var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
    var tokensToDelete = await _adminDb.UserSecurityTokens
        .Where(item => (item.UsedAt.HasValue && item.UsedAt.Value < cutoff)
            || (item.RevokedAt.HasValue && item.RevokedAt.Value < cutoff)
            || item.ExpiresAt < cutoff)
        .ToListAsync(ct);

    if (tokensToDelete.Count == 0)
    {
      return;
    }

    _adminDb.UserSecurityTokens.RemoveRange(tokensToDelete);
    await _adminDb.SaveChangesAsync(ct);
  }

  private async Task<GeneratedUserSecurityToken> GenerateTokenAsync(
      Guid tenantId,
      Guid userId,
      UserSecurityTokenPurpose purpose,
      int expirationHours,
      Guid? createdByUserId,
      CancellationToken ct)
  {
    try
    {
      await CleanupOldTokensAsync(ct);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Opportunistic user security token cleanup failed for user {UserId} and purpose {Purpose}", userId, purpose);
    }

    await RevokeActiveTokensAsync(userId, purpose, ct);

    var rawToken = GenerateRawToken();
    var now = DateTime.UtcNow;
    var token = new UserSecurityToken
    {
      Id = Guid.NewGuid(),
      TenantId = tenantId,
      UserId = userId,
      Purpose = purpose,
      TokenHash = ComputeTokenHash(rawToken),
      ExpiresAt = now.AddHours(expirationHours),
      CreatedAt = now,
      CreatedByUserId = createdByUserId
    };

    _adminDb.UserSecurityTokens.Add(token);
    await _adminDb.SaveChangesAsync(ct);

    return new GeneratedUserSecurityToken
    {
      TokenId = token.Id,
      RawToken = rawToken,
      ExpiresAt = token.ExpiresAt
    };
  }

  private static string GenerateRawToken()
  {
    Span<byte> bytes = stackalloc byte[32];
    RandomNumberGenerator.Fill(bytes);

    return Convert.ToBase64String(bytes)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
  }

  private static string ComputeTokenHash(string rawToken)
  {
    var bytes = Encoding.UTF8.GetBytes(rawToken);
    var hash = SHA256.HashData(bytes);
    return Convert.ToHexString(hash);
  }

  private static string? TrimOrNull(string? value, int maxLength)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return null;
    }

    var trimmed = value.Trim();
    return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
  }

  private static string GetErrorCode(UserSecurityTokenPurpose purpose, TokenValidationFailure failure)
  {
    return purpose switch
    {
      UserSecurityTokenPurpose.TenantAdminActivation => failure switch
      {
        TokenValidationFailure.Invalid => "INVALID_ACTIVATION_TOKEN",
        TokenValidationFailure.Expired => "EXPIRED_ACTIVATION_TOKEN",
        TokenValidationFailure.Used => "USED_ACTIVATION_TOKEN",
        TokenValidationFailure.Revoked => "REVOKED_ACTIVATION_TOKEN",
        _ => "INVALID_ACTIVATION_TOKEN"
      },
      UserSecurityTokenPurpose.PasswordReset => failure switch
      {
        TokenValidationFailure.Invalid => "INVALID_PASSWORD_RESET_TOKEN",
        TokenValidationFailure.Expired => "EXPIRED_PASSWORD_RESET_TOKEN",
        TokenValidationFailure.Used => "USED_PASSWORD_RESET_TOKEN",
        TokenValidationFailure.Revoked => "REVOKED_PASSWORD_RESET_TOKEN",
        _ => "INVALID_PASSWORD_RESET_TOKEN"
      },
      _ => "INVALID_SECURITY_TOKEN"
    };
  }

  private enum TokenValidationFailure
  {
    Invalid,
    Expired,
    Used,
    Revoked
  }
}

public sealed class GeneratedUserSecurityToken
{
  public Guid TokenId { get; init; }
  public string RawToken { get; init; } = string.Empty;
  public DateTime ExpiresAt { get; init; }
}

public sealed class UserSecurityTokenValidationResult
{
  public bool IsValid { get; init; }
  public string? ErrorCode { get; init; }
  public string Message { get; init; } = string.Empty;
  public UserSecurityToken? Token { get; init; }

  public static UserSecurityTokenValidationResult Success(UserSecurityToken token)
  {
    return new UserSecurityTokenValidationResult
    {
      IsValid = true,
      Token = token,
      Message = "Security token is valid."
    };
  }

  public static UserSecurityTokenValidationResult Fail(string errorCode, string message)
  {
    return new UserSecurityTokenValidationResult
    {
      IsValid = false,
      ErrorCode = errorCode,
      Message = message
    };
  }
}