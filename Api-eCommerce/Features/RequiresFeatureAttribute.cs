using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using CC.Infraestructure.Tenancy;

namespace Api_eCommerce.Features
{
 [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
 public class RequiresFeatureAttribute : Attribute, IAsyncActionFilter
 {
 private readonly string _featureCode;
 public RequiresFeatureAttribute(string featureCode) { _featureCode = featureCode; }

 public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
 {
 if (!context.HttpContext.Items.TryGetValue("TenantContext", out var obj) || obj is not TenantContext tc)
 {
 context.Result = new ObjectResult(new ProblemDetails{ Title = "Tenant not resolved" }){ StatusCode = StatusCodes.Status409Conflict };
 return;
 }
 var features = context.HttpContext.RequestServices.GetRequiredService<IFeatureService>();
 if (!await features.IsEnabledAsync(tc.Slug, _featureCode))
 {
 context.Result = new ObjectResult(new ProblemDetails{ Title = "Feature disabled", Detail = $"Feature '{_featureCode}' disabled for tenant '{tc.Slug}'" }){ StatusCode = StatusCodes.Status403Forbidden };
 return;
 }
 await next();
 }
 }
}