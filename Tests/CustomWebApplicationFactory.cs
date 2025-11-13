using CC.Infraestructure.AdminDb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api_eCommerce.Tests
{
    /// <summary>
    /// Factory personalizada para crear una aplicación de prueba con base de datos en memoria
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Remover el DbContext real
                services.RemoveAll(typeof(DbContextOptions<AdminDbContext>));
                services.RemoveAll(typeof(AdminDbContext));

                // Agregar DbContext con base de datos en memoria
                services.AddDbContext<AdminDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestAdminDb");
                });

                // Construir el service provider
                var sp = services.BuildServiceProvider();

                // Crear un scope y obtener el DbContext
                using var scope = sp.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<AdminDbContext>();

                // Asegurar que la base de datos está creada
                db.Database.EnsureCreated();

                // Seed inicial de datos de prueba
                SeedTestData(db);
            });

            builder.UseEnvironment("Testing");
        }

        private static void SeedTestData(AdminDbContext db)
        {
            // Limpiar datos existentes
            db.Tenants.RemoveRange(db.Tenants);
            db.SaveChanges();

            // Crear tenants de prueba
            var testTenants = new[]
            {
                new CC.Infraestructure.AdminDb.Entities.Tenant
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Slug = "test-tenant-1",
                    Name = "Test Tenant 1",
                    DbName = "tenant_test1_db",
                    Plan = "Premium",
                    Status = "Active",
                    AdminEmail = "admin@test1.com",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new CC.Infraestructure.AdminDb.Entities.Tenant
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Slug = "test-tenant-2",
                    Name = "Test Tenant 2",
                    DbName = "tenant_test2_db",
                    Plan = "Basic",
                    Status = "Active",
                    AdminEmail = "admin@test2.com",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new CC.Infraestructure.AdminDb.Entities.Tenant
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Slug = "test-tenant-pending",
                    Name = "Test Tenant Pending",
                    DbName = "tenant_pending_db",
                    Plan = "Basic",
                    Status = "Pending",
                    AdminEmail = "admin@pending.com",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            db.Tenants.AddRange(testTenants);
            db.SaveChanges();
        }
    }
}
