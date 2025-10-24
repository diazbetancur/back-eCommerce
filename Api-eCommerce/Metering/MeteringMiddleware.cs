using CC.Infraestructure.Admin;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Api_eCommerce.Middleware;

namespace Api_eCommerce.Metering
{
 public class MeteringMiddleware
 {
 private readonly RequestDelegate _next;
 public MeteringMiddleware(RequestDelegate next) { _next = next; }

 public async Task InvokeAsync(HttpContext context, AdminDbContext adminDb)
 {
 var start = DateTime.UtcNow;
 var operationId = Guid.NewGuid();
 context.Items["operationId"] = operationId;
 try
 {
 await _next(context);
 }
 catch
 {
 await IncrementAsync(context, adminDb, error: true);
 throw;
 }
 await IncrementAsync(context, adminDb);
 }

 private static async Task IncrementAsync(HttpContext context, AdminDbContext adminDb, bool error = false)
 {
 if (!context.Items.TryGetValue(TenantResolutionMiddleware.TenantContextKey, out var t) || t is not TenantContext tc)
 return;
 var today = DateOnly.FromDateTime(DateTime.UtcNow);
 var row = await adminDb.TenantUsageDaily.FirstOrDefaultAsync(x => x.TenantId == tc.TenantId && x.Date == today);
 if (row == null)
 {
 row = new TenantUsageDaily { TenantId = tc.TenantId, Date = today, ReqCount =0, ErrorCount =0, PushCount =0 };
 adminDb.TenantUsageDaily.Add(row);
 }
 row.ReqCount +=1;
 if (error) row.ErrorCount +=1;
 await adminDb.SaveChangesAsync();
 }
 }
}