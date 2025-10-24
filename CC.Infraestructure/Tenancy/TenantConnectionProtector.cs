using Microsoft.AspNetCore.DataProtection;

namespace CC.Infraestructure.Tenancy
{
 public interface ITenantConnectionProtector
 {
 string Protect(string plainConnectionString);
 string Unprotect(string protectedConnectionString);
 }

 public class TenantConnectionProtector : ITenantConnectionProtector
 {
 private readonly IDataProtector _protector;

 public TenantConnectionProtector(IDataProtectionProvider provider)
 {
 _protector = provider.CreateProtector("Tenancy.ConnectionStrings.v1");
 }

 public string Protect(string plainConnectionString)
 {
 return _protector.Protect(plainConnectionString);
 }

 public string Unprotect(string protectedConnectionString)
 {
 return _protector.Unprotect(protectedConnectionString);
 }
 }
}