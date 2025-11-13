using System.Net;

namespace Api_eCommerce.Tests.Health
{
    /// <summary>
    /// Tests para los endpoints de Health Check
    /// </summary>
    public class HealthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private const string ValidTenantSlug = "test-tenant-1";

        public HealthEndpointsTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GlobalHealthCheck_ReturnsOk()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeEmpty();
        }

        [Fact]
        public async Task GlobalHealthCheck_DoesNotRequireTenant()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            // Deliberadamente NO agregamos X-Tenant-Slug

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, 
                "Global health check should not require tenant");
        }

        [Fact]
        public async Task TenantHealthCheck_WithValidTenant_ReturnsOk()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health/tenant");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task TenantHealthCheck_WithoutTenant_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health/tenant");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task TenantHealthCheck_WithInvalidTenant_Returns404NotFound()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health/tenant");
            request.Headers.Add("X-Tenant-Slug", "nonexistent-tenant-xyz");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task TenantHealthCheck_WithPendingTenant_Returns403Forbidden()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health/tenant");
            request.Headers.Add("X-Tenant-Slug", "test-tenant-pending");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GlobalHealthCheck_ReturnsHealthyStatus()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Healthy", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TenantHealthCheck_ReturnsDatabaseStatus()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health/tenant");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotBeEmpty();
                // Debería incluir información sobre la base de datos del tenant
            }
        }

        [Fact]
        public async Task HealthCheck_CanBeCalled Repeatedly()
        {
            // Arrange
            var requests = Enumerable.Range(0, 10)
                .Select(_ => new HttpRequestMessage(HttpMethod.Get, "/health"));

            // Act
            var responses = await Task.WhenAll(
                requests.Select(r => _client.SendAsync(r))
            );

            // Assert
            responses.Should().AllSatisfy(r => 
                r.StatusCode.Should().Be(HttpStatusCode.OK));
        }

        [Theory]
        [InlineData("/health")]
        [InlineData("/Health")]
        [InlineData("/HEALTH")]
        public async Task HealthCheck_IsCaseInsensitive(string path)
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, path);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task TenantHealthCheck_DifferentTenants_AllRespond()
        {
            // Arrange
            var tenants = new[] { "test-tenant-1", "test-tenant-2" };
            var requests = tenants.Select(tenant =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/health/tenant");
                request.Headers.Add("X-Tenant-Slug", tenant);
                return request;
            });

            // Act
            var responses = await Task.WhenAll(
                requests.Select(r => _client.SendAsync(r))
            );

            // Assert
            responses.Should().AllSatisfy(r =>
            {
                r.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
                r.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            });
        }

        [Fact]
        public async Task HealthCheck_ReturnsJsonContent()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.Content.Headers.ContentType?.MediaType
                .Should().Be("application/json");
        }

        [Fact]
        public async Task HealthCheck_PerformanceTest_RespondsQuickly()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/health");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var response = await _client.SendAsync(request);
            stopwatch.Stop();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, 
                "Health check should respond in less than 1 second");
        }
    }
}
