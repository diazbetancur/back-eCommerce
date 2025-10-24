using CC.Infraestructure.Admin;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;

namespace Api_eCommerce.Endpoints
{
 public static class SuperAdminTenants
 {
 public static IEndpointRouteBuilder MapSuperAdminTenants(this IEndpointRouteBuilder app)
 {
 var group = app.MapGroup("/superadmin/tenants");

 group.MapPost("/", CreateTenant);
 group.MapPost("/repair", RepairTenant);
 //group.MapPost("/migrate-all", MigrateAll);

 return app;
 }

 private static async Task<IResult> CreateTenant(AdminDbContext adminDb, ITenantConnectionProtector protector, TenantDbContextFactory factory, string slug, string name, string planCode)
 {
 var regex = new Regex("^[a-z0-9-]{3,}$");
 if (!regex.IsMatch(slug)) return Results.ValidationProblem(new Dictionary<string, string[]>{{"slug", new[]{"invalid format"}}});

 var plan = await adminDb.Plans.FirstOrDefaultAsync(p => p.Code == planCode);
 if (plan == null) return Results.NotFound(new { errors = "plan not found" });

 var tenant = new CC.Infraestructure.Admin.Entities.Tenant{ Id = Guid.NewGuid(), Slug = slug, Name = name, Status = TenantStatus.Pending, PlanId = plan.Id, CreatedAt = DateTime.UtcNow };
 adminDb.Tenants.Add(tenant);
 await adminDb.SaveChangesAsync();

 try
 {
 var adminConn = adminDb.Database.GetConnectionString();
 var csb = new NpgsqlConnectionStringBuilder(adminConn);
 var dbName = $"tenant_{slug}";
 var masterCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString){ Database = "postgres" }.ToString();
 await using (var conn = new NpgsqlConnection(masterCs))
 {
 await conn.OpenAsync();
 await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\" WITH TEMPLATE template0 ENCODING 'UTF8';", conn);
 try { await cmd.ExecuteNonQueryAsync(); } catch (PostgresException pgEx) when (pgEx.SqlState == "42P04") { /* already exists */ }
 }

 tenant.Status = TenantStatus.Seeding; await adminDb.SaveChangesAsync();

 var tenantCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString){ Database = dbName }.ToString();
 await using(var tenantDb = factory.Create(tenantCs))
 {
 await tenantDb.Database.MigrateAsync();
 // Seed idempotente
 if (!await tenantDb.Roles.AnyAsync())
 {
 tenantDb.Roles.AddRange(new TenantRole{ Id=Guid.NewGuid(), Name="Admin"}, new TenantRole{ Id=Guid.NewGuid(), Name="Manager"}, new TenantRole{ Id=Guid.NewGuid(), Name="Viewer"});
 }
 if (!await tenantDb.Users.AnyAsync())
 {
 var tempPass = Guid.NewGuid().ToString("N").Substring(0,10);
 var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
 var hash = hasher.HashPassword(null!, tempPass);
 tenantDb.Users.Add(new TenantUser{ Id=Guid.NewGuid(), Email=$"admin@{slug}", PasswordHash = hash, IsActive = true });
 }
 await tenantDb.SaveChangesAsync();
 }

 tenant.EncryptedConnection = protector.Protect(tenantCs);
 tenant.Status = TenantStatus.Ready; tenant.LastError = null; tenant.UpdatedAt = DateTime.UtcNow;
 await adminDb.SaveChangesAsync();
 return Results.Created($"/superadmin/tenants/{slug}", new { slug, status = "Ready" });
 }
 catch(Exception ex)
 {
 tenant.Status = TenantStatus.Failed; tenant.LastError = ex.Message; tenant.UpdatedAt = DateTime.UtcNow; await adminDb.SaveChangesAsync();
 return Results.Problem(statusCode:500, detail: ex.Message);
 }
 }

 private static async Task<IResult> RepairTenant(AdminDbContext adminDb, ITenantConnectionProtector protector, TenantDbContextFactory factory, string tenant)
 {
 var t = await adminDb.Tenants.FirstOrDefaultAsync(x => x.Slug == tenant);
 if (t == null) return Results.NotFound();
 try
 {
 var cs = protector.Unprotect(t.EncryptedConnection);
 await using(var tenantDb = factory.Create(cs))
 {
 await tenantDb.Database.MigrateAsync();
 }
 t.Status = TenantStatus.Ready; t.LastError = null; t.UpdatedAt = DateTime.UtcNow; await adminDb.SaveChangesAsync();
 return Results.Ok(new { tenant, status = "Ready" });
 }
 catch(Exception ex)
 {
 t.Status = TenantStatus.Failed; t.LastError = ex.Message; t.UpdatedAt = DateTime.UtcNow; await adminDb.SaveChangesAsync();
 return Results.Problem(statusCode:500, detail: ex.Message);
 }
 }
 }
}