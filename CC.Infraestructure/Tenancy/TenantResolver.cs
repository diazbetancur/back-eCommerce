using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CC.Infraestructure.Tenancy
{
  public class TenantResolver : ITenantResolver
  {
    private readonly AdminDbContext _adminDb;
    private readonly ITenantSecretProtector _protector;
    private readonly TenantSecretsOptions _tenantSecretsOptions;
    private readonly string? _tenantDbTemplate;

    public TenantResolver(
      AdminDbContext adminDb,
      ITenantSecretProtector protector,
      IOptions<TenantSecretsOptions> tenantSecretsOptions,
      IConfiguration configuration)
    {
      _adminDb = adminDb;
      _protector = protector;
      _tenantSecretsOptions = tenantSecretsOptions.Value;
      _tenantDbTemplate = configuration["Tenancy:TenantDbTemplate"];
    }

    public async Task<TenantContext?> ResolveAsync(HttpContext httpContext)
    {
      var slug = httpContext.Request.Headers["X-Tenant-Slug"].FirstOrDefault()
      ?? httpContext.Request.Query["tenant"].FirstOrDefault()
      ?? httpContext.Request.Host.Host.Split('.').FirstOrDefault();

      if (string.IsNullOrWhiteSpace(slug)) return null;

      var tenant = await _adminDb.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
      if (tenant == null) return null;

      if (tenant.Status != TenantStatus.Ready)
      {
        httpContext.Response.StatusCode = StatusCodes.Status423Locked;
        var payload = JsonSerializer.Serialize(new { errors = $"Tenant '{slug}' is not ready. Status={tenant.Status}" });
        await httpContext.Response.WriteAsync(payload);
        return null;
      }

      if (string.IsNullOrWhiteSpace(tenant.EncryptedConnection))
      {
        throw new InvalidOperationException($"Tenant '{tenant.Slug}' has no encrypted connection string.");
      }

      string cs;

      try
      {
        cs = _protector.Decrypt(tenant.EncryptedConnection);
      }
      catch (TenantSecretProtectionException)
      {
        if (string.IsNullOrWhiteSpace(_tenantDbTemplate) || !_tenantDbTemplate.Contains("{DbName}"))
        {
          throw;
        }

        cs = _tenantDbTemplate.Replace("{DbName}", tenant.DbName);
        tenant.EncryptedConnection = _protector.Encrypt(cs);
        tenant.EncryptionKeyId = _tenantSecretsOptions.KeyId;
        tenant.EncryptionAlgorithm = _tenantSecretsOptions.Algorithm;
        tenant.EncryptionVersion = _tenantSecretsOptions.Version;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _adminDb.SaveChangesAsync();
      }

      return new TenantContext { TenantId = tenant.Id, Slug = tenant.Slug, PlanId = tenant.PlanId, ConnectionString = cs };
    }
  }
}