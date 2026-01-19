namespace CC.Domain.Dto
{
  /// <summary>
  /// DTOs para autenticación de tenants
  /// </summary>
  public record TenantLoginRequest(
      string Email,
      string Password
  );

  public record TenantAuthResponse(
      string Token,
      DateTime ExpiresAt,
      TenantUserDto User
  );

  public record TenantUserDto(
      Guid Id,
      string Email,
      List<string> Roles,
      List<ModulePermissionDto> Permissions,
      bool IsActive,
      DateTime CreatedAt
  );

  public record ModulePermissionDto
  {
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? IconName { get; set; }
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
  }

  public class ModuleDto
  {
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public ModulePermissions Permissions { get; set; } = new();
  }

  public class ModulePermissions
  {
    public string ModuleCode { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
  }
}
