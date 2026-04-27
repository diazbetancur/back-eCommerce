using System.Net;
using System.Net.Http.Json;

namespace Api_eCommerce.Tests.Tenancy;

public class PublicTenantConfigTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly HttpClient _client;

  public PublicTenantConfigTests(CustomWebApplicationFactory factory)
  {
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task GetPublicTenantConfig_WithPendingActivationAndTenantHeader_Returns200WithShowAndActivationStatus()
  {
    var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/tenant/test-tenant-pending");
    request.Headers.Add("X-Tenant-Slug", "test-tenant-pending");

    var response = await _client.SendAsync(request);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var payload = await response.Content.ReadFromJsonAsync<PublicTenantConfigTestResponse>();
    payload.Should().NotBeNull();
    payload!.Show.Should().BeTrue();
    payload.ActivationStatus.Should().Be("PendingActivation");
    payload.Tenant.Should().NotBeNull();
    payload.Tenant!.Slug.Should().Be("test-tenant-pending");
    payload.Tenant.Status.Should().Be("PendingActivation");
  }

  [Theory]
  [InlineData("test-tenant-1", "Active", true)]
  [InlineData("test-tenant-suspended", "Suspended", true)]
  [InlineData("test-tenant-disabled", "Disabled", true)]
  public async Task GetPublicTenantConfig_WithPublicStatuses_ReturnsTenantContext(string slug, string activationStatus, bool show)
  {
    var response = await _client.GetAsync($"/api/public/tenant/{slug}");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var payload = await response.Content.ReadFromJsonAsync<PublicTenantConfigTestResponse>();
    payload.Should().NotBeNull();
    payload!.Show.Should().Be(show);
    payload.ActivationStatus.Should().Be(activationStatus);
    payload.Tenant.Should().NotBeNull();
    payload.Tenant!.Slug.Should().Be(slug);
    payload.Tenant.Status.Should().Be(activationStatus);
  }

  [Fact]
  public async Task GetPublicTenantConfig_WithDeletedTenant_Returns404()
  {
    var response = await _client.GetAsync("/api/public/tenant/test-tenant-deleted");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GetPublicTenantConfig_WithMissingTenant_Returns404()
  {
    var response = await _client.GetAsync("/api/public/tenant/nonexistent-tenant");

    response.StatusCode.Should().Be(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Login_WithPendingActivationTenant_RemainsBlockedByMiddleware()
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/auth/login")
    {
      Content = JsonContent.Create(new
      {
        email = "admin@example.com",
        password = "Secret123!"
      })
    };
    request.Headers.Add("X-Tenant-Slug", "test-tenant-pending");

    var response = await _client.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();

    response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    content.Should().Contain("Tenant Not Available");
  }

  [Fact]
  public async Task ActivateAccount_WithPendingActivationTenant_IsNotBlockedByMiddleware()
  {
    var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/activate-account")
    {
      Content = JsonContent.Create(new
      {
        token = "invalid-token",
        password = "Secret123!",
        confirmPassword = "Secret123!"
      })
    };
    request.Headers.Add("X-Tenant-Slug", "test-tenant-pending");

    var response = await _client.SendAsync(request);

    response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
  }

  private sealed class PublicTenantConfigTestResponse
  {
    public TenantInfoTestResponse? Tenant { get; set; }
    public bool Show { get; set; }
    public string ActivationStatus { get; set; } = string.Empty;
  }

  private sealed class TenantInfoTestResponse
  {
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
  }
}