namespace CC.Domain.Dto
{
  /// <summary>
  /// DTOs para módulos y permisos
  /// </summary>
  public record ModulesResponse
  {
    public List<ModuleResponse> Modules { get; set; } = new();
  }

  public record ModuleResponse
  {
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public PermissionsResponse Permissions { get; set; } = new();
  }

  public record PermissionsResponse
  {
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanUpdate { get; set; }
    public bool CanDelete { get; set; }
  }
}
