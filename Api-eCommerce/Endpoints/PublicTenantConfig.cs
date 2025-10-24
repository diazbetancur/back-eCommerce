using CC.Infraestructure.Admin;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api_eCommerce.Endpoints
{
 public static class PublicTenantConfig
 {
 public static IEndpointRouteBuilder MapPublicTenantConfig(this IEndpointRouteBuilder app)
 {
 app.MapGet("/public/tenant-config", async (HttpContext http, AdminDbContext adminDb, ITenantResolver resolver) =>
 {
 var ctx = await resolver.ResolveAsync(http);
 if (ctx == null) return Results.Problem(statusCode:409, detail: "Tenant not resolved or not ready");
 var t = await adminDb.Tenants.Include(x => x.Plan).AsNoTracking().FirstAsync(x => x.Slug == ctx.Slug);
 var features = await adminDb.PlanFeatures.Where(pf => pf.PlanId == t.PlanId).Select(pf => pf.Feature.Code).ToListAsync();
 return Results.Ok(new { name = t.Name, slug = t.Slug, theme = new { }, seo = new { }, features });
 });
 return app;
 }
 }
}