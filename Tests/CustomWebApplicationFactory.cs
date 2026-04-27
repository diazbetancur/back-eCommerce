using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Admin.Entities;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api_eCommerce.Tests
{
    /// <summary>
    /// Factory personalizada para crear una aplicaci�n de prueba con base de datos en memoria
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _testDbName = $"TestAdminDb_{Guid.NewGuid():N}";

        public CustomWebApplicationFactory()
        {
            const string testJwtKey = "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF";
            Environment.SetEnvironmentVariable("JWT__Key", testJwtKey);
            Environment.SetEnvironmentVariable("Jwt__SigningKey", testJwtKey);
            Environment.SetEnvironmentVariable("jwtKey", testJwtKey);
            Environment.SetEnvironmentVariable("Jwt__Issuer", "ecommerce-api");
            Environment.SetEnvironmentVariable("Jwt__Audience", "ecommerce-clients");
            Environment.SetEnvironmentVariable("Jwt__StrictValidation", "true");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF",
                    ["Jwt:SigningKey"] = "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF",
                    ["jwtKey"] = "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF",
                    ["Jwt:Issuer"] = "ecommerce-api",
                    ["Jwt:Audience"] = "ecommerce-clients",
                    ["Jwt:StrictValidation"] = "true",
                    ["TenantSecrets:MasterKey"] = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                    ["TenantSecrets:KeyId"] = "test-shared-key",
                    ["TenantSecrets:Algorithm"] = "AES-256-GCM",
                    ["TenantSecrets:Version"] = "v1",
                    ["Tenancy:TenantDbTemplate"] = "Host=localhost;Port=5432;Database={DbName};Username=test;Password=test"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Remover el DbContext real
                services.RemoveAll(typeof(DbContextOptions<AdminDbContext>));
                services.RemoveAll(typeof(AdminDbContext));

                // Agregar DbContext con base de datos en memoria
                services.AddDbContext<AdminDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_testDbName);
                });

                // Construir el service provider
                var sp = services.BuildServiceProvider();

                // Crear un scope y obtener el DbContext
                using var scope = sp.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<AdminDbContext>();

                // Asegurar que la base de datos est� creada
                db.Database.EnsureCreated();

                // Seed inicial de datos de prueba
                SeedTestData(db, scopedServices);
            });

            builder.UseEnvironment("Testing");
        }

        private static void SeedTestData(AdminDbContext db, IServiceProvider services)
        {
            // Limpiar datos existentes
            db.Tenants.RemoveRange(db.Tenants);
            db.Plans.RemoveRange(db.Plans);
            db.SaveChanges();

            var protector = services.GetRequiredService<ITenantSecretProtector>();

            var premiumPlan = new Plan
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Code = "premium",
                Name = "Premium"
            };

            var basicPlan = new Plan
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Code = "basic",
                Name = "Basic"
            };

            db.Plans.AddRange(premiumPlan, basicPlan);
            db.SaveChanges();

            // Crear tenants de prueba
            var testTenants = new[]
            {
                new Tenant
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Slug = "test-tenant-1",
                    Name = "Test Tenant 1",
                    DbName = "tenant_test1_db",
                    PlanId = premiumPlan.Id,
                    Status = TenantStatus.Active,
                    EncryptedConnection = protector.Encrypt("Host=localhost;Port=5432;Database=tenant_test1_db;Username=test;Password=test"),
                    EncryptionKeyId = "test-shared-key",
                    EncryptionAlgorithm = "AES-256-GCM",
                    EncryptionVersion = "v1",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Tenant
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Slug = "test-tenant-2",
                    Name = "Test Tenant 2",
                    DbName = "tenant_test2_db",
                    PlanId = basicPlan.Id,
                    Status = TenantStatus.Active,
                    EncryptedConnection = protector.Encrypt("Host=localhost;Port=5432;Database=tenant_test2_db;Username=test;Password=test"),
                    EncryptionKeyId = "test-shared-key",
                    EncryptionAlgorithm = "AES-256-GCM",
                    EncryptionVersion = "v1",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Tenant
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Slug = "test-tenant-pending",
                    Name = "Test Tenant Pending",
                    DbName = "tenant_pending_db",
                    PlanId = basicPlan.Id,
                    Status = TenantStatus.PendingActivation,
                    EncryptedConnection = protector.Encrypt("Host=localhost;Port=5432;Database=tenant_pending_db;Username=test;Password=test"),
                    EncryptionKeyId = "test-shared-key",
                    EncryptionAlgorithm = "AES-256-GCM",
                    EncryptionVersion = "v1",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Tenant
                {
                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    Slug = "test-tenant-suspended",
                    Name = "Test Tenant Suspended",
                    DbName = "tenant_suspended_db",
                    PlanId = premiumPlan.Id,
                    Status = TenantStatus.Suspended,
                    EncryptedConnection = protector.Encrypt("Host=localhost;Port=5432;Database=tenant_suspended_db;Username=test;Password=test"),
                    EncryptionKeyId = "test-shared-key",
                    EncryptionAlgorithm = "AES-256-GCM",
                    EncryptionVersion = "v1",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Tenant
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    Slug = "test-tenant-disabled",
                    Name = "Test Tenant Disabled",
                    DbName = "tenant_disabled_db",
                    PlanId = basicPlan.Id,
                    Status = TenantStatus.Disabled,
                    EncryptedConnection = protector.Encrypt("Host=localhost;Port=5432;Database=tenant_disabled_db;Username=test;Password=test"),
                    EncryptionKeyId = "test-shared-key",
                    EncryptionAlgorithm = "AES-256-GCM",
                    EncryptionVersion = "v1",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Tenant
                {
                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    Slug = "test-tenant-deleted",
                    Name = "Test Tenant Deleted",
                    DbName = "tenant_deleted_db",
                    PlanId = basicPlan.Id,
                    Status = TenantStatus.Deleted,
                    EncryptedConnection = protector.Encrypt("Host=localhost;Port=5432;Database=tenant_deleted_db;Username=test;Password=test"),
                    EncryptionKeyId = "test-shared-key",
                    EncryptionAlgorithm = "AES-256-GCM",
                    EncryptionVersion = "v1",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            db.Tenants.AddRange(testTenants);
            db.SaveChanges();
        }
    }
}
