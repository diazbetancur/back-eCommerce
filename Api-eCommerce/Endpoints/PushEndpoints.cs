using Api_eCommerce.Features;
using Api_eCommerce.Push;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api_eCommerce.Endpoints
{
 public static class PushEndpoints
 {
 public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
 {
 var group = app.MapGroup("/api/push");
 group.MapPost("/subscribe", Subscribe);
 group.MapPost("/send", Send)
 .AddEndpointFilter(new RequiresFeatureFilter("push", app.ServiceProvider))
 .AddEndpointFilter(new RequiresQuotaFilter("push_daily_quota", app.ServiceProvider));
 return app;
 }

 private static async Task<IResult> Subscribe(HttpContext http, ITenantResolver resolver, TenantDbContextFactory factory, SubscriptionDto dto)
 {
 var ctx = await resolver.ResolveAsync(http);
 if (ctx == null) return Results.Problem(statusCode:409, detail: "Tenant not resolved or not ready");
 await using var db = factory.Create(ctx.ConnectionString);
 var existing = await db.WebPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
 if (existing == null)
 {
 db.WebPushSubscriptions.Add(new CC.Infraestructure.Tenant.Entities.WebPushSubscription{ Id = Guid.NewGuid(), Endpoint = dto.Endpoint, P256dh = dto.P256dh, Auth = dto.Auth, UserAgent = dto.UserAgent });
 }
 else
 {
 existing.P256dh = dto.P256dh; existing.Auth = dto.Auth; existing.UserAgent = dto.UserAgent;
 }
 await db.SaveChangesAsync();
 return Results.NoContent();
 }

 private static async Task<IResult> Send(HttpContext http, ITenantResolver resolver, TenantDbContextFactory factory, IWebPushSender sender, object payload)
 {
 var ctx = await resolver.ResolveAsync(http);
 if (ctx == null) return Results.Problem(statusCode:409, detail: "Tenant not resolved or not ready");
 await using var db = factory.Create(ctx.ConnectionString);
 var subs = await db.WebPushSubscriptions.Select(s => new ValueTuple<string,string,string>(s.Endpoint, s.P256dh, s.Auth)).ToListAsync();
 var n = await sender.SendAsync(subs, payload);
 return Results.Accepted("/api/push/send", new { recipients = n, accepted = true });
 }

 public record SubscriptionDto(string Endpoint, string P256dh, string Auth, string UserAgent);
 }

 // Endpoint filter wrappers to reuse attribute-style checks in Minimal APIs
 public class RequiresFeatureFilter : IEndpointFilter
 {
 private readonly string _featureCode;
 private readonly IServiceProvider _sp;
 public RequiresFeatureFilter(string featureCode, IServiceProvider sp) { _featureCode = featureCode; _sp = sp; }
 public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
 {
 var resolver = context.HttpContext.RequestServices.GetRequiredService<ITenantResolver>();
 var tc = await resolver.ResolveAsync(context.HttpContext);
 if (tc == null) return Results.Problem(statusCode:409, detail: "Tenant not resolved or not ready");
 var features = context.HttpContext.RequestServices.GetRequiredService<Api_eCommerce.Features.IFeatureService>();
 if (!await features.IsEnabledAsync(tc.Slug, _featureCode))
 return Results.Problem(statusCode:403, title: "Feature disabled", detail: $"Feature '{_featureCode}' disabled for tenant '{tc.Slug}'");
 return await next(context);
 }
 }

 public class RequiresQuotaFilter : IEndpointFilter
 {
 private readonly string _featureCode;
 private readonly IServiceProvider _sp;
 public RequiresQuotaFilter(string featureCode, IServiceProvider sp) { _featureCode = featureCode; _sp = sp; }
 public async ValueTask<object> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
 {
 var resolver = context.HttpContext.RequestServices.GetRequiredService<ITenantResolver>();
 var tc = await resolver.ResolveAsync(context.HttpContext);
 if (tc == null) return Results.Problem(statusCode:409, detail: "Tenant not resolved or not ready");
 var features = context.HttpContext.RequestServices.GetRequiredService<Api_eCommerce.Features.IFeatureService>();
 var limit = await features.GetLimitAsync(tc.Slug, _featureCode);
 if (limit == null || limit <=0)
 return Results.Problem(statusCode:403, title: "Quota exceeded or not configured", detail: $"Quota '{_featureCode}' not available for tenant '{tc.Slug}'");
 return await next(context);
 }
 }
}