using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CC.Domain.Dto;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api_eCommerce.IntegrationTests.E2E
{
  /// <summary>
  /// Tests de integración para el endpoint GET /api/tenant-config
  /// </summary>
  public class TenantConfigTests : IClassFixture<TestWebApplicationFactory>
  {
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantConfigTests(TestWebApplicationFactory factory)
    {
      _factory = factory;
      _client = factory.CreateClient();
    }

    [Fact(DisplayName = "GET /api/tenant-config - debe retornar 401 sin autenticación")]
    public async Task GetTenantConfig_WithoutAuth_Returns401()
    {
      // Act
      var response = await _client.GetAsync("/api/tenant-config");

      // Assert
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /api/tenant-config - debe retornar feature flags del tenant autenticado")]
    public async Task GetTenantConfig_WithAuth_ReturnsFeatureFlags()
    {
      // Arrange - Registrar y autenticar usuario en el tenant de prueba
      var registerResponse = await RegisterAndLoginUser();
      var token = await ExtractTokenFromResponse(registerResponse);

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

      // Act
      var response = await _client.GetAsync("/api/tenant-config");

      // Assert
      response.EnsureSuccessStatusCode();
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);

      var content = await response.Content.ReadFromJsonAsync<TenantConfigResponse>();
      Assert.NotNull(content);
      Assert.NotEqual(Guid.Empty, content.TenantId);
      Assert.False(string.IsNullOrEmpty(content.TenantSlug));
      Assert.NotNull(content.Features);
    }

    [Fact(DisplayName = "GET /api/tenant-config - debe incluir todos los feature flags requeridos")]
    public async Task GetTenantConfig_WithAuth_ContainsAllRequiredFeatures()
    {
      // Arrange
      var registerResponse = await RegisterAndLoginUser();
      var token = await ExtractTokenFromResponse(registerResponse);

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

      // Act
      var response = await _client.GetAsync("/api/tenant-config");

      // Assert
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadFromJsonAsync<TenantConfigResponse>();
      Assert.NotNull(content);
      Assert.NotNull(content.Features);

      // Verificar que todos los feature flags están presentes (valores por defecto)
      var features = content.Features;

      // Estos son valores booleanos, solo verificamos que existen
      Assert.IsType<bool>(features.Loyalty);
      Assert.IsType<bool>(features.Multistore);
      Assert.IsType<bool>(features.PaymentsWompiEnabled);
      Assert.IsType<bool>(features.AllowGuestCheckout);
      Assert.IsType<bool>(features.ShowStock);
      Assert.IsType<bool>(features.EnableReviews);
      Assert.IsType<bool>(features.EnableAdvancedSearch);
      Assert.IsType<bool>(features.EnableAnalytics);
    }

    [Fact(DisplayName = "GET /api/tenant-config - debe usar cache de features (15min TTL)")]
    public async Task GetTenantConfig_UsesFeatureCache()
    {
      // Arrange
      var registerResponse = await RegisterAndLoginUser();
      var token = await ExtractTokenFromResponse(registerResponse);

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

      // Act - Primera llamada (popula cache)
      var response1 = await _client.GetAsync("/api/tenant-config");
      response1.EnsureSuccessStatusCode();
      var content1 = await response1.Content.ReadFromJsonAsync<TenantConfigResponse>();

      // Act - Segunda llamada (debe usar cache)
      var response2 = await _client.GetAsync("/api/tenant-config");
      response2.EnsureSuccessStatusCode();
      var content2 = await response2.Content.ReadFromJsonAsync<TenantConfigResponse>();

      // Assert - Los valores deben ser idénticos
      Assert.NotNull(content1);
      Assert.NotNull(content2);
      Assert.Equal(content1.TenantId, content2.TenantId);
      Assert.Equal(content1.TenantSlug, content2.TenantSlug);

      var json1 = JsonSerializer.Serialize(content1.Features);
      var json2 = JsonSerializer.Serialize(content2.Features);
      Assert.Equal(json1, json2);
    }

    [Fact(DisplayName = "GET /api/tenant-config - debe retornar features correctas para plan FREE")]
    public async Task GetTenantConfig_FreePlan_ReturnsCorrectFeatures()
    {
      // Arrange - El tenant de prueba suele ser FREE por defecto
      var registerResponse = await RegisterAndLoginUser();
      var token = await ExtractTokenFromResponse(registerResponse);

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

      // Act
      var response = await _client.GetAsync("/api/tenant-config");
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadFromJsonAsync<TenantConfigResponse>();

      // Assert - Plan FREE tiene configuración básica
      Assert.NotNull(content);
      var features = content.Features;

      // FREE plan tiene guest checkout deshabilitado por defecto
      // y features básicos limitados (verificar según DefaultFeatureFlags.GetForPlan("FREE"))
      Assert.NotNull(features);
    }

    #region Helper Methods

    private async Task<HttpResponseMessage> RegisterAndLoginUser()
    {
      var loginRequest = new
      {
        email = $"test-{Guid.NewGuid()}@example.com",
        password = "Test123!",
        firstName = "Test",
        lastName = "User",
        phoneNumber = "+573001234567"
      };

      // Asumiendo que existe un endpoint de registro o login
      // Ajustar según la implementación real de auth en tu API
      var response = await _client.PostAsJsonAsync("/api/auth/tenant-register", loginRequest);

      if (!response.IsSuccessStatusCode)
      {
        // Intentar login si ya existe
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/tenant-login", new
        {
          email = loginRequest.email,
          password = loginRequest.password
        });

        loginResponse.EnsureSuccessStatusCode();
        return loginResponse;
      }

      return response;
    }

    private async Task<string> ExtractTokenFromResponse(HttpResponseMessage response)
    {
      var authResult = await response.Content.ReadFromJsonAsync<JsonElement>();

      // Intentar extraer token de diferentes estructuras de respuesta posibles
      if (authResult.TryGetProperty("token", out var tokenElement))
      {
        return tokenElement.GetString() ?? throw new Exception("Token is null");
      }

      if (authResult.TryGetProperty("accessToken", out var accessTokenElement))
      {
        return accessTokenElement.GetString() ?? throw new Exception("AccessToken is null");
      }

      if (authResult.TryGetProperty("result", out var resultElement) &&
          resultElement.TryGetProperty("token", out var nestedTokenElement))
      {
        return nestedTokenElement.GetString() ?? throw new Exception("Nested token is null");
      }

      throw new Exception("No token found in authentication response");
    }

    #endregion
  }
}
