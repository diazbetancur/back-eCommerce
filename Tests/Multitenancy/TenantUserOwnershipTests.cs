using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api_eCommerce.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tests.Multitenancy
{
  /// <summary>
  /// Tests de validación de tenant-user ownership
  /// Garantiza que un usuario no pueda acceder a datos de otro tenant
  /// </summary>
  public class TenantUserOwnershipTests : IClassFixture<CustomWebApplicationFactory>
  {
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantUserOwnershipTests(CustomWebApplicationFactory factory)
    {
      _factory = factory;
      _client = factory.CreateClient(new WebApplicationFactoryClientOptions
      {
        AllowAutoRedirect = false
      });
    }

    /// <summary>
    /// Test 1: Usuario de tenant A intentando acceder a tenant B debe recibir 403
    /// </summary>
    [Fact]
    public async Task User_From_TenantA_Accessing_TenantB_Should_Return403()
    {
      // Arrange - Crear tenant A con usuario
      var tenantASlug = "tenant-a-" + Guid.NewGuid().ToString()[..8];
      var tenantBSlug = "tenant-b-" + Guid.NewGuid().ToString()[..8];

      // Provisionar tenant A
      var tenantA = await ProvisionTenant(tenantASlug, "Tenant A Test", "Basic");
      Assert.NotNull(tenantA);

      // Provisionar tenant B
      var tenantB = await ProvisionTenant(tenantBSlug, "Tenant B Test", "Basic");
      Assert.NotNull(tenantB);

      // Registrar usuario en tenant A
      var userA = await RegisterUser(tenantASlug, "usera@test.com", "Password123!", "User", "A");
      Assert.NotNull(userA?.Token);

      // Act - Intentar acceder a endpoint de tenant B con JWT de tenant A
      var request = new HttpRequestMessage(HttpMethod.Get, "/me/orders");
      request.Headers.Add("X-Tenant-Slug", tenantBSlug); // ⚠️ Tenant B
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userA.Token); // JWT de tenant A

      var response = await _client.SendAsync(request);

      // Assert
      Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

      var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
      Assert.NotNull(error);
      Assert.Contains("Token Tenant Mismatch", error.Error);
      Assert.Equal(tenantASlug, error.JwtTenant);
      Assert.Equal(tenantBSlug, error.RequestedTenant);
    }

    /// <summary>
    /// Test 2: Usuario de tenant A accediendo a tenant A debe funcionar (200)
    /// </summary>
    [Fact]
    public async Task User_From_TenantA_Accessing_TenantA_Should_Return200()
    {
      // Arrange - Crear tenant A con usuario
      var tenantSlug = "tenant-valid-" + Guid.NewGuid().ToString()[..8];

      var tenant = await ProvisionTenant(tenantSlug, "Valid Tenant Test", "Basic");
      Assert.NotNull(tenant);

      var user = await RegisterUser(tenantSlug, "validuser@test.com", "Password123!", "Valid", "User");
      Assert.NotNull(user?.Token);

      // Act - Acceder a endpoint del mismo tenant
      var request = new HttpRequestMessage(HttpMethod.Get, "/me/orders");
      request.Headers.Add("X-Tenant-Slug", tenantSlug); // ✅ Mismo tenant
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

      var response = await _client.SendAsync(request);

      // Assert
      Assert.True(
          response.StatusCode == HttpStatusCode.OK ||
          response.StatusCode == HttpStatusCode.NotFound, // OK si no hay órdenes
          $"Expected 200 or 404, but got {response.StatusCode}");
    }

    /// <summary>
    /// Test 3: Request guest (sin JWT) a /api/cart debe funcionar (200)
    /// NO debe bloquearse por el middleware de ownership
    /// </summary>
    [Fact]
    public async Task Guest_Request_To_Cart_Without_JWT_Should_Return200()
    {
      // Arrange - Crear tenant para cart público
      var tenantSlug = "tenant-guest-" + Guid.NewGuid().ToString()[..8];

      var tenant = await ProvisionTenant(tenantSlug, "Guest Cart Tenant", "Basic");
      Assert.NotNull(tenant);

      var sessionId = Guid.NewGuid().ToString();

      // Act - Request guest (sin Authorization header)
      var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
      request.Headers.Add("X-Tenant-Slug", tenantSlug);
      request.Headers.Add("X-Session-Id", sessionId); // Guest checkout

      var response = await _client.SendAsync(request);

      // Assert
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);

      var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
      Assert.NotNull(cart);
      Assert.Empty(cart.Items); // Carrito vacío para nueva sesión
    }

    // ==================== HELPERS ====================

    private async Task<TenantProvisionResponse?> ProvisionTenant(string slug, string name, string plan)
    {
      var initRequest = new
      {
        slug,
        name,
        plan
      };

      var initResponse = await _client.PostAsJsonAsync("/provision/tenants/init", initRequest);

      if (!initResponse.IsSuccessStatusCode)
      {
        var error = await initResponse.Content.ReadAsStringAsync();
        throw new Exception($"Failed to init tenant: {error}");
      }

      var initResult = await initResponse.Content.ReadFromJsonAsync<InitProvisionResponse>();
      Assert.NotNull(initResult?.ConfirmToken);

      // Confirmar aprovisionamiento
      var confirmRequest = new { confirmToken = initResult.ConfirmToken };
      var confirmResponse = await _client.PostAsJsonAsync("/provision/tenants/confirm", confirmRequest);

      if (!confirmResponse.IsSuccessStatusCode)
      {
        var error = await confirmResponse.Content.ReadAsStringAsync();
        throw new Exception($"Failed to confirm tenant: {error}");
      }

      var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ConfirmProvisionResponse>();
      Assert.NotNull(confirmResult?.ProvisioningId);

      // Esperar a que el tenant esté Ready (polling)
      for (int i = 0; i < 30; i++) // Max 30 segundos
      {
        await Task.Delay(1000);

        var statusResponse = await _client.GetAsync($"/provision/tenants/{confirmResult.ProvisioningId}/status");
        if (statusResponse.IsSuccessStatusCode)
        {
          var status = await statusResponse.Content.ReadFromJsonAsync<ProvisionStatusResponse>();
          if (status?.TenantStatus == "Ready")
          {
            return new TenantProvisionResponse
            {
              TenantId = status.TenantId,
              Slug = slug
            };
          }
        }
      }

      throw new Exception($"Tenant {slug} provisioning timeout");
    }

    private async Task<RegisterResponse?> RegisterUser(
        string tenantSlug,
        string email,
        string password,
        string firstName,
        string lastName)
    {
      var registerRequest = new
      {
        email,
        password,
        firstName,
        lastName,
        phoneNumber = "+573001234567"
      };

      var request = new HttpRequestMessage(HttpMethod.Post, "/auth/register");
      request.Headers.Add("X-Tenant-Slug", tenantSlug);
      request.Content = JsonContent.Create(registerRequest);

      var response = await _client.SendAsync(request);

      if (!response.IsSuccessStatusCode)
      {
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception($"Failed to register user: {error}");
      }

      return await response.Content.ReadFromJsonAsync<RegisterResponse>();
    }

    // ==================== DTOs ====================

    private record TenantProvisionResponse
    {
      public Guid TenantId { get; init; }
      public string Slug { get; init; } = string.Empty;
    }

    private record InitProvisionResponse(string ConfirmToken);
    private record ConfirmProvisionResponse(Guid ProvisioningId);
    private record ProvisionStatusResponse(Guid TenantId, string TenantStatus);

    private record RegisterResponse(string Token, DateTime ExpiresAt);

    private record ErrorResponse
    {
      public string Error { get; init; } = string.Empty;
      public string JwtTenant { get; init; } = string.Empty;
      public string RequestedTenant { get; init; } = string.Empty;
    }

    private record CartResponse(List<CartItem> Items);
    private record CartItem(Guid Id, string ProductName, decimal Price, int Quantity);
  }
}
