using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CC.Infraestructure.Tenancy;

namespace Api_eCommerce.Features
{
 [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
 public class RequiresQuotaAttribute : Attribute, IAsyncActionFilter
 {
 private readonly string _featureCode;
 public RequiresQuotaAttribute(string featureCode) { _featureCode = featureCode; }

 public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
 {
 if (!context.HttpContext.Items.TryGetValue("TenantContext", out var obj) || obj is not TenantContext tc)
 {
 context.Result = new ObjectResult(new ProblemDetails{ Title = "Tenant not resolved" }){ StatusCode = StatusCodes.Status409Conflict };
 return;
 }
 var features = context.HttpContext.RequestServices.GetRequiredService<IFeatureService>();
 var limit = await features.GetLimitAsync(tc.Slug, _featureCode);
 if (limit == null || limit <=0)
 {
 context.Result = new ObjectResult(new ProblemDetails{ Title = "Quota exceeded or not configured", Detail = $"Quota '{_featureCode}' not available for tenant '{tc.Slug}'" }){ StatusCode = StatusCodes.Status403Forbidden };
 return;
 }
 await next();
 }
 }
}