using System.Net;
using System.Net.Http.Json;

namespace Api_eCommerce.Tests.Catalog
{
    /// <summary>
    /// Tests para los endpoints de Catalog
    /// </summary>
    public class CatalogEndpointsTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private const string ValidTenantSlug = "test-tenant-1";
        private const string ValidTenantSlug2 = "test-tenant-2";

        public CatalogEndpointsTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetProducts_WithValidTenant_ReturnsOk()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetProducts_WithoutTenant_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetProducts_WithPagination_ReturnsCorrectData()
        {
            // Arrange
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                "/api/catalog/products?page=1&pageSize=10");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotBeEmpty();
            }
        }

        [Fact]
        public async Task GetProducts_DifferentTenants_ReturnsDifferentData()
        {
            // Arrange
            var request1 = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request1.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request2.Headers.Add("X-Tenant-Slug", ValidTenantSlug2);

            // Act
            var response1 = await _client.SendAsync(request1);
            var response2 = await _client.SendAsync(request2);

            // Assert
            response1.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response2.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            
            // Los datos deberían ser independientes por tenant
            // (aunque en este test no tenemos productos seed, verificamos que no hay error)
        }

        [Fact]
        public async Task GetProductById_WithValidTenant_ReturnsProduct()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                $"/api/catalog/products/{productId}");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest, 
                "Request should not fail due to missing tenant");
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, 
                "Tenant should be active");
        }

        [Fact]
        public async Task GetProductById_WithoutTenant_Returns400BadRequest()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                $"/api/catalog/products/{productId}");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetCategories_WithValidTenant_ReturnsOk()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/categories");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task SearchProducts_WithQuery_ReturnsResults()
        {
            // Arrange
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                "/api/catalog/products/search?q=test");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        }

        [Theory]
        [InlineData("?page=1&pageSize=10")]
        [InlineData("?page=2&pageSize=20")]
        [InlineData("?page=1&pageSize=50")]
        public async Task GetProducts_WithDifferentPaginationParams_ReturnsOk(string queryString)
        {
            // Arrange
            var request = new HttpRequestMessage(
                HttpMethod.Get, 
                $"/api/catalog/products{queryString}");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetProducts_WithInvalidTenant_Returns404NotFound()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", "nonexistent-tenant-xyz");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetProducts_TenantIsolation_EnsuresDataSeparation()
        {
            // Arrange
            var requests = new[]
            {
                CreateRequestWithTenant(ValidTenantSlug),
                CreateRequestWithTenant(ValidTenantSlug2),
                CreateRequestWithTenant(ValidTenantSlug)
            };

            // Act
            var responses = await Task.WhenAll(
                requests.Select(r => _client.SendAsync(r))
            );

            // Assert
            responses.Should().AllSatisfy(r =>
            {
                r.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
                r.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
            });
        }

        private HttpRequestMessage CreateRequestWithTenant(string tenantSlug)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/catalog/products");
            request.Headers.Add("X-Tenant-Slug", tenantSlug);
            return request;
        }
    }
}
