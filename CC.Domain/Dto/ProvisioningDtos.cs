namespace CC.Domain.Dto
{
  /// <summary>
  /// DTOs para el proceso de aprovisionamiento de tenants
  /// </summary>
  public record InitProvisioningRequest(string Slug, string Name, string Plan);

  public record InitProvisioningResponse(Guid ProvisioningId, string ConfirmToken, string Next, string Message);

  public record ConfirmProvisioningResponse(Guid ProvisioningId, string Status, string Message, string StatusEndpoint);

  public record ProvisioningStatusResponse(string Status, string? TenantSlug, string? DbName, List<ProvisioningStepDto> Steps);

  /// <summary>
  /// DTO para los pasos del aprovisionamiento
  /// </summary>
  public class ProvisioningStepDto
  {
    public string Step { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Log { get; set; }
    public string? ErrorMessage { get; set; }
  }
}
