using CC.Infraestructure.AdminDb;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api_eCommerce.Tests.Tenancy
{
    /// <summary>
    /// Tests para el TenantResolutionMiddleware y resolución de tenants
    /// </summary>
    public class TenantResolverTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public TenantResolverTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task ResolveAsync_WithValidHeaderSlug_ReturnsTenantContext()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", "test-tenant-1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            // Si el tenant se resolvió, no debería ser 400 (Bad Request) ni 404 (Not Found)
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ResolveAsync_WithValidQueryParameter_ReturnsTenantContext()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products?tenant=test-tenant-1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ResolveAsync_WithoutTenantSlug_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Tenant slug");
        }

        [Fact]
        public async Task ResolveAsync_WithInvalidSlug_Returns404NotFound()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", "nonexistent-tenant");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Tenant not found");
        }

        [Fact]
        public async Task ResolveAsync_WithPendingTenant_Returns403Forbidden()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", "test-tenant-pending");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("not available");
        }

        [Fact]
        public async Task ResolveAsync_HeaderTakesPriorityOverQuery()
        {
            // Arrange
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                "/api/catalog/products?tenant=test-tenant-2");
            request.Headers.Add("X-Tenant-Slug", "test-tenant-1");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            // Debería usar el tenant del header (test-tenant-1)
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound);
        }

        [Theory]
        [InlineData("/swagger")]
        [InlineData("/health")]
        [InlineData("/provision/tenants/init")]
        [InlineData("/superadmin/tenants")]
        public async Task ResolveAsync_ExcludedPaths_DoNotRequireTenant(string path)
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            // Deliberadamente NO agregamos X-Tenant-Slug

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            // No debería ser 400 por falta de tenant
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.BadRequest, 
                $"Path {path} should not require tenant header");
        }

        [Fact]
        public async Task ResolveAsync_CaseInsensitiveSlug_ResolvesCorrectly()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", "TEST-TENANT-1"); // Mayúsculas

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound, 
                "Slug should be case-insensitive");
        }

        [Fact]
        public async Task ResolveAsync_WithWhitespaceSlug_TrimsAndResolves()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", "  test-tenant-1  "); // Con espacios

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(System.Net.HttpStatusCode.NotFound, 
                "Slug should be trimmed");
        }

        [Fact]
        public async Task TenantAccessor_ShouldStoreResolvedTenant()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantAccessor>();
            var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

            // Simular resolución de tenant
            var tenant = await adminDb.Tenants.FirstAsync(t => t.Slug == "test-tenant-1");
            var tenantInfo = new TenantInfo
            {
                Id = tenant.Id,
                Slug = tenant.Slug,
                DbName = tenant.DbName,
                Plan = tenant.Plan,
                ConnectionString = $"Host=localhost;Database={tenant.DbName}"
            };

            // Act
            tenantAccessor.SetTenant(tenantInfo);

            // Assert
            tenantAccessor.HasTenant.Should().BeTrue();
            tenantAccessor.TenantInfo.Should().NotBeNull();
            tenantAccessor.TenantInfo!.Slug.Should().Be("test-tenant-1");
            tenantAccessor.TenantInfo.Plan.Should().Be("Premium");
        }

        [Fact]
        public void TenantAccessor_BeforeResolution_HasNoTenant()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantAccessor>();

            // Act & Assert
            tenantAccessor.HasTenant.Should().BeFalse();
            tenantAccessor.TenantInfo.Should().BeNull();
        }
    }
}
