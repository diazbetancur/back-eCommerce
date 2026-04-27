using CC.Aplication.Notifications;
using CC.Domain.Notifications;
using CC.Domain.SecurityTokens;
using CC.Domain.Users;
using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Globalization;

namespace CC.Aplication.Auth;

public interface ITenantAccountSecurityService
{
  Task<TenantAdminActivationDispatchResult> CreateTenantAdminActivationAsync(
  TenantAdminActivationDispatchRequest request,
      CancellationToken ct = default);

  Task<AccountSecurityOperationResult> ActivateAccountAsync(
      string rawToken,
      string password,
      string confirmPassword,
      string? ipAddress,
      string? userAgent,
      CancellationToken ct = default);

  Task<AccountSecurityOperationResult> RequestPasswordAssistanceAsync(string email, CancellationToken ct = default);

  Task<AccountSecurityOperationResult> ResetPasswordAsync(
      string rawToken,
      string password,
      string confirmPassword,
      string? ipAddress,
      string? userAgent,
      CancellationToken ct = default);
}

public sealed class TenantAdminActivationDispatchRequest
{
  public Guid TenantId { get; init; }
  public Guid UserId { get; init; }
  public string TenantSlug { get; init; } = string.Empty;
  public string TenantName { get; init; } = string.Empty;
  public string AdminEmail { get; init; } = string.Empty;
  public string AdminName { get; init; } = string.Empty;
  public Guid? CreatedByUserId { get; init; }
}

public sealed class TenantAccountSecurityService : ITenantAccountSecurityService
{
  private const string GenericAccountHelpMessage = "Si la cuenta existe, enviaremos instrucciones al correo asociado.";

  private readonly AdminDbContext _adminDb;
  private readonly TenantDbContextFactory _tenantDbFactory;
  private readonly ITenantAccessor _tenantAccessor;
  private readonly IUserSecurityTokenService _userSecurityTokenService;
  private readonly INotificationDispatcher _notificationDispatcher;
  private readonly SecurityTokenOptions _options;
  private readonly EmailOptions _emailOptions;
  private readonly ILogger<TenantAccountSecurityService> _logger;

  public TenantAccountSecurityService(
      AdminDbContext adminDb,
      TenantDbContextFactory tenantDbFactory,
      ITenantAccessor tenantAccessor,
      IUserSecurityTokenService userSecurityTokenService,
      INotificationDispatcher notificationDispatcher,
      TenantAccountSecuritySettings settings,
      ILogger<TenantAccountSecurityService> logger)
  {
    _adminDb = adminDb;
    _tenantDbFactory = tenantDbFactory;
    _tenantAccessor = tenantAccessor;
    _userSecurityTokenService = userSecurityTokenService;
    _notificationDispatcher = notificationDispatcher;
    _options = settings.SecurityTokens;
    _emailOptions = settings.Email;
    _logger = logger;
  }

  public async Task<TenantAdminActivationDispatchResult> CreateTenantAdminActivationAsync(
      TenantAdminActivationDispatchRequest request,
      CancellationToken ct = default)
  {
    var generatedToken = await _userSecurityTokenService.GenerateTenantAdminActivationTokenAsync(request.TenantId, request.UserId, request.CreatedByUserId, ct);
    var activationUrl = BuildActivateAccountUrl(request.TenantSlug, generatedToken.RawToken);
    var replyTo = ResolveReplyTo(request.AdminEmail);

    try
    {
      var dispatchResult = await _notificationDispatcher.DispatchAsync(new NotificationDispatchRequest
      {
        TenantId = request.TenantId,
        EventCode = NotificationEventCodes.TenantAdminActivation,
        Channel = NotificationChannel.Email,
        Recipient = request.AdminEmail,
        FromName = ResolveFromName(request.TenantName),
        ReplyTo = replyTo,
        Variables = new Dictionary<string, string?>
        {
          ["tenantName"] = request.TenantName,
          ["adminName"] = request.AdminName,
          ["activationUrl"] = activationUrl,
          ["supportEmail"] = replyTo,
          ["expirationHours"] = _options.ActivationTokenExpirationHours.ToString(CultureInfo.InvariantCulture)
        },
        ReferenceType = "tenant",
        ReferenceId = request.TenantId.ToString()
      }, ct);

      return new TenantAdminActivationDispatchResult
      {
        Success = true,
        NotificationAccepted = dispatchResult.Accepted,
        Message = dispatchResult.Message ?? "Activation token generated successfully."
      };
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Tenant admin activation notification failed for tenant {TenantId}", request.TenantId);

      if (_emailOptions.FailOnProviderError)
      {
        throw;
      }

      return new TenantAdminActivationDispatchResult
      {
        Success = true,
        NotificationAccepted = false,
        Message = "Activation token generated, but notification dispatch failed."
      };
    }
  }

  public async Task<AccountSecurityOperationResult> ActivateAccountAsync(
      string rawToken,
      string password,
      string confirmPassword,
      string? ipAddress,
      string? userAgent,
      CancellationToken ct = default)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return AccountSecurityOperationResult.Fail("TENANT_REQUIRED", "Tenant header is required.");
    }

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
    {
      return AccountSecurityOperationResult.Fail("PASSWORD_CONFIRMATION_MISMATCH", "Password confirmation does not match.");
    }

    if (!MeetsPasswordPolicy(password))
    {
      return AccountSecurityOperationResult.Fail("PASSWORD_POLICY_NOT_MET", "Password does not meet the minimum policy.");
    }

    var validation = await _userSecurityTokenService.ValidateTokenAsync(rawToken, UserSecurityTokenPurpose.TenantAdminActivation, ct);
    if (!validation.IsValid || validation.Token == null)
    {
      return AccountSecurityOperationResult.Fail(validation.ErrorCode ?? "INVALID_ACTIVATION_TOKEN", validation.Message);
    }

    var tenantInfo = _tenantAccessor.TenantInfo;
    if (validation.Token.TenantId != tenantInfo.Id)
    {
      return AccountSecurityOperationResult.Fail("TENANT_MISMATCH", "The activation token is not valid for the current tenant.");
    }

    var tenant = await _adminDb.Tenants.FirstOrDefaultAsync(item => item.Id == tenantInfo.Id, ct);
    if (tenant == null)
    {
      return AccountSecurityOperationResult.Fail("INVALID_ACTIVATION_TOKEN", "The activation token is not valid.");
    }

    await using var tenantDb = _tenantDbFactory.Create();
    var user = await tenantDb.Users.FirstOrDefaultAsync(item => item.Id == validation.Token.UserId, ct);
    if (user == null)
    {
      return AccountSecurityOperationResult.Fail("INVALID_ACTIVATION_TOKEN", "The activation token is not valid.");
    }

    if (user.Status == UserStatus.Active)
    {
      tenant.Status = TenantStatus.Active;
      tenant.PrimaryAdminUserId ??= user.Id;
      tenant.PrimaryAdminEmail ??= user.Email;
      tenant.UpdatedAt = DateTime.UtcNow;
      await _userSecurityTokenService.ConsumeTokenAsync(validation.Token.Id, ipAddress, userAgent, ct);

      return AccountSecurityOperationResult.Succeed("Cuenta activada correctamente.");
    }

    if (user.Status != UserStatus.PendingActivation)
    {
      return AccountSecurityOperationResult.Fail("USER_NOT_PENDING_ACTIVATION", "The user is not pending activation.");
    }

    if (tenant.Status != TenantStatus.PendingActivation && tenant.Status != TenantStatus.Active)
    {
      return AccountSecurityOperationResult.Fail("TENANT_NOT_PENDING_ACTIVATION", "The tenant is not pending activation.");
    }

    try
    {
      var passwordHasher = new PasswordHasher<User>();
      user.PasswordHash = passwordHasher.HashPassword(user, password);
      user.Status = UserStatus.Active;
      user.IsActive = true;
      user.MustChangePassword = false;
      user.UpdatedAt = DateTime.UtcNow;

      await tenantDb.SaveChangesAsync(ct);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to synchronize tenant user activation for tenant {TenantId}", tenantInfo.Id);
      return AccountSecurityOperationResult.Fail("TENANT_SYNC_FAILED", "The account could not be synchronized with the tenant database.");
    }

    tenant.Status = TenantStatus.Active;
    tenant.PrimaryAdminUserId ??= user.Id;
    tenant.PrimaryAdminEmail ??= user.Email;
    tenant.UpdatedAt = DateTime.UtcNow;
    await _userSecurityTokenService.ConsumeTokenAsync(validation.Token.Id, ipAddress, userAgent, ct);

    return AccountSecurityOperationResult.Succeed("Cuenta activada correctamente.");
  }

  public async Task<AccountSecurityOperationResult> RequestPasswordAssistanceAsync(string email, CancellationToken ct = default)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return AccountSecurityOperationResult.Succeed(GenericAccountHelpMessage);
    }

    if (string.IsNullOrWhiteSpace(email))
    {
      return AccountSecurityOperationResult.Succeed(GenericAccountHelpMessage);
    }

    var normalizedEmail = email.Trim().ToLowerInvariant();
    await using var tenantDb = _tenantDbFactory.Create();
    var user = await tenantDb.Users
        .AsNoTracking()
        .FirstOrDefaultAsync(item => item.Email == normalizedEmail, ct);

    if (user == null)
    {
      return AccountSecurityOperationResult.Succeed(GenericAccountHelpMessage);
    }

    var tenant = await _adminDb.Tenants.FirstOrDefaultAsync(item => item.Id == _tenantAccessor.TenantInfo.Id, ct);
    if (tenant == null)
    {
      return AccountSecurityOperationResult.Succeed(GenericAccountHelpMessage);
    }

    if (user.Status == UserStatus.PendingActivation && tenant.PrimaryAdminUserId == user.Id)
    {
      await CreateTenantAdminActivationAsync(new TenantAdminActivationDispatchRequest
      {
        TenantId = tenant.Id,
        UserId = user.Id,
        TenantSlug = tenant.Slug,
        TenantName = tenant.Name,
        AdminEmail = user.Email,
        AdminName = GetDisplayName(user)
      }, ct);

      return AccountSecurityOperationResult.Succeed(GenericAccountHelpMessage);
    }

    if (user.Status == UserStatus.Active)
    {
      var generatedToken = await _userSecurityTokenService.GeneratePasswordResetTokenAsync(tenant.Id, user.Id, null, ct);
      var resetPasswordUrl = BuildResetPasswordUrl(tenant.Slug, generatedToken.RawToken);
      var displayName = GetDisplayName(user);
      var replyTo = ResolveReplyTo(tenant.PrimaryAdminEmail);

      try
      {
        await _notificationDispatcher.DispatchAsync(new NotificationDispatchRequest
        {
          TenantId = tenant.Id,
          EventCode = NotificationEventCodes.PasswordReset,
          Channel = NotificationChannel.Email,
          Recipient = user.Email,
          FromName = ResolveFromName(tenant.Name),
          ReplyTo = replyTo,
          Variables = new Dictionary<string, string?>
          {
            ["tenantName"] = tenant.Name,
            ["userName"] = displayName,
            ["customerName"] = displayName,
            ["resetPasswordUrl"] = resetPasswordUrl,
            ["actionUrl"] = resetPasswordUrl,
            ["supportEmail"] = replyTo,
            ["expirationHours"] = _options.PasswordResetTokenExpirationHours.ToString(CultureInfo.InvariantCulture)
          },
          ReferenceType = "tenant-user",
          ReferenceId = user.Id.ToString()
        }, ct);
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Password reset notification failed for tenant {TenantId} and user {UserId}", tenant.Id, user.Id);

        if (_emailOptions.FailOnProviderError)
        {
          throw;
        }
      }
    }

    return AccountSecurityOperationResult.Succeed(GenericAccountHelpMessage);
  }

  public async Task<AccountSecurityOperationResult> ResetPasswordAsync(
      string rawToken,
      string password,
      string confirmPassword,
      string? ipAddress,
      string? userAgent,
      CancellationToken ct = default)
  {
    if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
    {
      return AccountSecurityOperationResult.Fail("TENANT_REQUIRED", "Tenant header is required.");
    }

    if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
    {
      return AccountSecurityOperationResult.Fail("PASSWORD_CONFIRMATION_MISMATCH", "Password confirmation does not match.");
    }

    if (!MeetsPasswordPolicy(password))
    {
      return AccountSecurityOperationResult.Fail("PASSWORD_POLICY_NOT_MET", "Password does not meet the minimum policy.");
    }

    var validation = await _userSecurityTokenService.ValidateTokenAsync(rawToken, UserSecurityTokenPurpose.PasswordReset, ct);
    if (!validation.IsValid || validation.Token == null)
    {
      return AccountSecurityOperationResult.Fail(validation.ErrorCode ?? "INVALID_PASSWORD_RESET_TOKEN", validation.Message);
    }

    var tenantInfo = _tenantAccessor.TenantInfo;
    if (validation.Token.TenantId != tenantInfo.Id)
    {
      return AccountSecurityOperationResult.Fail("TENANT_MISMATCH", "The password reset token is not valid for the current tenant.");
    }

    await using var tenantDb = _tenantDbFactory.Create();
    var user = await tenantDb.Users.FirstOrDefaultAsync(item => item.Id == validation.Token.UserId, ct);
    if (user == null)
    {
      return AccountSecurityOperationResult.Fail("INVALID_PASSWORD_RESET_TOKEN", "The password reset token is not valid.");
    }

    if (user.Status != UserStatus.Active)
    {
      return AccountSecurityOperationResult.Fail("USER_NOT_ACTIVE", "The user is not active.");
    }

    var passwordHasher = new PasswordHasher<User>();
    user.PasswordHash = passwordHasher.HashPassword(user, password);
    user.MustChangePassword = false;
    user.UpdatedAt = DateTime.UtcNow;
    await tenantDb.SaveChangesAsync(ct);

    await _userSecurityTokenService.ConsumeTokenAsync(validation.Token.Id, ipAddress, userAgent, ct);

    return AccountSecurityOperationResult.Succeed("Contraseña actualizada correctamente.");
  }

  private static bool MeetsPasswordPolicy(string password)
  {
    return !string.IsNullOrWhiteSpace(password) && password.Trim().Length >= 8;
  }

  private static string GetDisplayName(User user)
  {
    var firstName = user.FirstName?.Trim();
    var lastName = user.LastName?.Trim();
    var fullName = string.Join(" ", new[] { firstName, lastName }.Where(item => !string.IsNullOrWhiteSpace(item)));
    return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
  }

  private string BuildActivateAccountUrl(string tenantSlug, string rawToken)
  {
    return BuildTenantPublicUrl(tenantSlug, _emailOptions.ActivationPath, rawToken);
  }

  private string BuildResetPasswordUrl(string tenantSlug, string rawToken)
  {
    return BuildTenantPublicUrl(tenantSlug, _emailOptions.ResetPasswordPath, rawToken);
  }

  private string BuildTenantPublicUrl(string tenantSlug, string path, string rawToken)
  {
    var domain = _emailOptions.PublicBaseDomain.Trim().Trim('.');
    var normalizedPath = path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
    return $"https://{tenantSlug}.{domain}{normalizedPath}?token={Uri.EscapeDataString(rawToken)}";
  }

  private string ResolveFromName(string? tenantName)
  {
    return string.IsNullOrWhiteSpace(tenantName) ? _emailOptions.FromName : tenantName.Trim();
  }

  private string ResolveReplyTo(string? preferredEmail)
  {
    if (IsValidEmail(preferredEmail))
    {
      return preferredEmail!.Trim();
    }

    return _emailOptions.SupportEmail;
  }

  private static bool IsValidEmail(string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return false;
    }

    try
    {
      _ = new MailAddress(value.Trim());
      return true;
    }
    catch (FormatException)
    {
      return false;
    }
  }
}

public sealed class AccountSecurityOperationResult
{
  public bool Success { get; init; }
  public string Message { get; init; } = string.Empty;
  public string? ErrorCode { get; init; }

  public static AccountSecurityOperationResult Succeed(string message)
  {
    return new AccountSecurityOperationResult
    {
      Success = true,
      Message = message
    };
  }

  public static AccountSecurityOperationResult Fail(string errorCode, string message)
  {
    return new AccountSecurityOperationResult
    {
      Success = false,
      ErrorCode = errorCode,
      Message = message
    };
  }
}

public sealed class TenantAdminActivationDispatchResult
{
  public bool Success { get; init; }
  public bool NotificationAccepted { get; init; }
  public string Message { get; init; } = string.Empty;
}