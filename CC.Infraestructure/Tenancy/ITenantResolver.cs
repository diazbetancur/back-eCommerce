using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace CC.Infraestructure.Tenancy
{
 public interface ITenantResolver
 {
 Task<TenantContext?> ResolveAsync(HttpContext httpContext);
 }
}