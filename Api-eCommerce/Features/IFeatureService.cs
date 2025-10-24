using System.Threading.Tasks;

namespace Api_eCommerce.Features
{
 public interface IFeatureService
 {
 Task<bool> IsEnabledAsync(string tenantSlug, string featureCode);
 Task<int?> GetLimitAsync(string tenantSlug, string featureCode);
 }
}