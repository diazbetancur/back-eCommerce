using CC.Infraestructure.Tenancy;

namespace Api_eCommerce.Middleware
{
 public class TenantResolutionMiddleware
 {
 private readonly RequestDelegate _next;
 public const string TenantContextKey = "TenantContext";

 public TenantResolutionMiddleware(RequestDelegate next)
 {
 _next = next;
 }

 public async Task InvokeAsync(HttpContext context, ITenantResolver resolver)
 {
 var tenant = await resolver.ResolveAsync(context);
 if (tenant != null)
 {
 context.Items[TenantContextKey] = tenant;
 }
 await _next(context);
 }
 }
}
