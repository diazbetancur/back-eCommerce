using CC.Infraestructure.AdminDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CC.Infraestructure.DesignTime;

public class AdminDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
  public AdminDbContext CreateDbContext(string[] args)
  {
    var optionsBuilder = new DbContextOptionsBuilder<AdminDbContext>();
    optionsBuilder.UseNpgsql("Host=localhost;Database=admin_template;Username=postgres;Password=postgres",
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "admin"));

    return new AdminDbContext(optionsBuilder.Options);
  }
}