using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api_eCommerce.IntegrationTests.E2E
{
    /// <summary>
    /// Tests end-to-end del flujo de provisioning de tenants
    /// </summary>
    public class ProvisioningTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ProvisioningTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task ProvisionTenant_CompleteFlow_ShouldSucceed()
        {
            // ==================== STEP 1: INITIALIZE ====================
            // Arrange
            var tenantSlug = $"test-{Guid.NewGuid():N}";
            var initRequest = new
            {
                slug = tenantSlug,
                name = "Test E2E Store",
                plan = "Premium"
            };

            // Act: Initialize provisioning
            var initResponse = await _client.PostAsJsonAsync("/provision/tenants/init", initRequest);

            // Assert: Should return 200 with confirm token
            initResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var initResult = await initResponse.Content.ReadFromJsonAsync<InitProvisioningResponse>();
            initResult.Should().NotBeNull();
            initResult!.ProvisioningId.Should().NotBeEmpty();
            initResult.ConfirmToken.Should().NotBeNullOrEmpty();
            initResult.Next.Should().Be("/provision/tenants/confirm");

            // ==================== STEP 2: CONFIRM ====================
            // Act: Confirm provisioning with token
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", initResult.ConfirmToken);
            
            var confirmResponse = await _client.PostAsync("/provision/tenants/confirm", null);

            // Assert: Should queue provisioning
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ConfirmProvisioningResponse>();
            confirmResult.Should().NotBeNull();
            confirmResult!.Status.Should().Be("QUEUED");
            confirmResult.StatusEndpoint.Should().Contain(initResult.ProvisioningId.ToString());

            // Remove authorization header for status check
            _client.DefaultRequestHeaders.Authorization = null;

            // ==================== STEP 3: POLL STATUS ====================
            // Act: Check status (with retry)
            var statusResponse = await PollProvisioningStatusAsync(initResult.ProvisioningId);

            // Assert: Should eventually be ready
            statusResponse.Should().NotBeNull();
            statusResponse!.Status.Should().Be("Ready");
            statusResponse.TenantSlug.Should().Be(tenantSlug);
            statusResponse.DbName.Should().NotBeNullOrEmpty();
            
            // Assert: All steps should be successful
            statusResponse.Steps.Should().NotBeEmpty();
            statusResponse.Steps.Should().AllSatisfy(step =>
            {
                step.Status.Should().Be("Success");
                step.CompletedAt.Should().NotBeNull();
                step.ErrorMessage.Should().BeNull();
            });

            // ==================== STEP 4: VERIFY TENANT ====================
            // Act: Get tenant configuration
            _client.DefaultRequestHeaders.Add("X-Tenant-Slug", tenantSlug);
            var configResponse = await _client.GetAsync("/public/tenant-config");

            // Assert: Should return tenant configuration
            configResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var config = await configResponse.Content.ReadFromJsonAsync<TenantConfigResponse>();
            config.Should().NotBeNull();
            config!.Slug.Should().Be(tenantSlug);
            config.Name.Should().Be("Test E2E Store");
            config.Features.Should().Contain("catalog");
        }

        [Fact]
        public async Task ProvisionTenant_DuplicateSlug_ShouldFail()
        {
            // Arrange: Create tenant first time
            var tenantSlug = $"test-dup-{Guid.NewGuid():N}";
            var initRequest = new
            {
                slug = tenantSlug,
                name = "First Tenant",
                plan = "Premium"
            };

            var firstResponse = await _client.PostAsJsonAsync("/provision/tenants/init", initRequest);
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act: Try to create tenant with same slug
            var duplicateRequest = new
            {
                slug = tenantSlug,
                name = "Duplicate Tenant",
                plan = "Basic"
            };

            var duplicateResponse = await _client.PostAsJsonAsync("/provision/tenants/init", duplicateRequest);

            // Assert: Should return 409 Conflict
            duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task ProvisionTenant_InvalidSlug_ShouldFail()
        {
            // Arrange
            var initRequest = new
            {
                slug = "INVALID_SLUG_WITH_UPPERCASE",
                name = "Invalid Tenant",
                plan = "Premium"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/provision/tenants/init", initRequest);

            // Assert: Should return 400 Bad Request
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ProvisionTenant_InvalidPlan_ShouldFail()
        {
            // Arrange
            var initRequest = new
            {
                slug = $"test-{Guid.NewGuid():N}",
                name = "Test Tenant",
                plan = "NonExistentPlan"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/provision/tenants/init", initRequest);

            // Assert: Should return 400 Bad Request
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task ConfirmProvisioning_WithoutToken_ShouldFail()
        {
            // Act: Try to confirm without authorization header
            var response = await _client.PostAsync("/provision/tenants/confirm", null);

            // Assert: Should return 401 Unauthorized
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ConfirmProvisioning_WithExpiredToken_ShouldFail()
        {
            // Arrange: Use an expired/invalid token
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", "expired.invalid.token");

            // Act
            var response = await _client.PostAsync("/provision/tenants/confirm", null);

            // Assert: Should return 401 Unauthorized
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // ==================== HELPER METHODS ====================

        private async Task<ProvisioningStatusResponse?> PollProvisioningStatusAsync(
            Guid provisioningId,
            int maxAttempts = 10,
            int delayMs = 1000)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                var response = await _client.GetAsync($"/provision/tenants/{provisioningId}/status");
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get provisioning status: {response.StatusCode}");
                }

                var status = await response.Content.ReadFromJsonAsync<ProvisioningStatusResponse>();
                
                if (status?.Status == "Ready" || status?.Status == "Failed")
                {
                    return status;
                }

                // Wait before next attempt
                await Task.Delay(delayMs);
            }

            throw new TimeoutException($"Provisioning did not complete after {maxAttempts} attempts");
        }

        // ==================== DTOs ====================

        private record InitProvisioningResponse(
            Guid ProvisioningId,
            string ConfirmToken,
            string Next,
            string Message
        );

        private record ConfirmProvisioningResponse(
            Guid ProvisioningId,
            string Status,
            string Message,
            string StatusEndpoint
        );

        private record ProvisioningStatusResponse(
            string Status,
            string? TenantSlug,
            string? DbName,
            List<ProvisioningStepDto> Steps
        );

        private record ProvisioningStepDto(
            string Step,
            string Status,
            DateTime StartedAt,
            DateTime? CompletedAt,
            string? Log,
            string? ErrorMessage
        );

        private record TenantConfigResponse(
            string Name,
            string Slug,
            object Theme,
            object Seo,
            List<string> Features
        );
    }
}
