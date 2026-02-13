using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Api_eCommerce.Metering
{
    public class MeteringMiddleware
    {
        private readonly RequestDelegate _next;

        public MeteringMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, AdminDbContext adminDb, ITenantAccessor tenantAccessor)
        {
            // Ignorar peticiones OPTIONS (CORS preflight)
            if (context.Request.Method == "OPTIONS")
            {
                await _next(context);
                return;
            }

            var start = DateTime.UtcNow;
            var operationId = Guid.NewGuid();
            context.Items["operationId"] = operationId;

            try
            {
                await _next(context);
            }
            catch
            {
                await IncrementAsync(tenantAccessor, adminDb, error: true);
                throw;
            }

            await IncrementAsync(tenantAccessor, adminDb);
        }

        private static async Task IncrementAsync(ITenantAccessor tenantAccessor, AdminDbContext adminDb, bool error = false)
        {
            // Solo registrar m�tricas si hay un tenant resuelto
            if (!tenantAccessor.HasTenant || tenantAccessor.TenantInfo == null)
                return;

            var tenantId = tenantAccessor.TenantInfo.Id;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var row = await adminDb.TenantProvisionings
                .Where(x => x.TenantId == tenantId)
                .FirstOrDefaultAsync();

            // TODO: Implementar l�gica de metering real cuando TenantUsageDaily est� disponible
            // Por ahora solo logueamos

            // if (row == null)
            // {
            //     row = new TenantUsageDaily 
            //     { 
            //         TenantId = tenantId, 
            //         Date = today, 
            //         ReqCount = 0, 
            //         ErrorCount = 0, 
            //         PushCount = 0 
            //     };
            //     adminDb.TenantUsageDaily.Add(row);
            // }

            // row.ReqCount += 1;
            // if (error) 
            //     row.ErrorCount += 1;

            // await adminDb.SaveChangesAsync();
        }
    }
}