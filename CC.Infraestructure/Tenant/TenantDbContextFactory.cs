using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CC.Infraestructure.Tenant
{
 public class TenantDbContextFactory
 {
 private readonly IServiceProvider _sp;
 public TenantDbContextFactory(IServiceProvider sp) { _sp = sp; }

 public TenantDbContext Create(string connectionString)
 {
 var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
 optionsBuilder.UseNpgsql(connectionString);
 return new TenantDbContext(optionsBuilder.Options);
 }
 }
}