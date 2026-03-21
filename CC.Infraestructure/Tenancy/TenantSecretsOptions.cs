namespace CC.Infraestructure.Tenancy;

public sealed class TenantSecretsOptions
{
  public const string SectionName = "TenantSecrets";

  public string MasterKey { get; set; } = string.Empty;
  public string KeyId { get; set; } = "tenant-master-key-dev-qa";
  public string Algorithm { get; set; } = "AES-256-GCM";
  public string Version { get; set; } = "v1";
}
