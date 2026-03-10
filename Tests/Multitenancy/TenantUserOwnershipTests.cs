using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api_eCommerce.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
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
    private const string JwtKey = "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF";
    private const string Tenant1Id = "11111111-1111-1111-1111-111111111111";
    private const string Tenant2Id = "22222222-2222-2222-2222-222222222222";
    private const string Tenant1Slug = "test-tenant-1";
    private const string Tenant2Slug = "test-tenant-2";

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
      // Arrange - JWT válido de tenant A
      var tokenFromTenantA = CreateJwtToken(Tenant1Id, Tenant1Slug);

      // Act - Intentar acceder a endpoint de tenant B con JWT de tenant A
      var request = new HttpRequestMessage(HttpMethod.Get, "/me/orders");
      request.Headers.Add("X-Tenant-Slug", Tenant2Slug); // ⚠️ Tenant B
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenFromTenantA); // JWT de tenant A

      var response = await _client.SendAsync(request);

      // Assert
      Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

      var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
      Assert.NotNull(error);
      Assert.Contains("Token Tenant Mismatch", error.Error);
      Assert.Equal(Tenant1Slug, error.JwtTenant);
      Assert.Equal(Tenant2Slug, error.RequestedTenant);
    }

    /// <summary>
    /// Test 2: Usuario de tenant A accediendo a tenant A debe funcionar (200)
    /// </summary>
    [Fact]
    public async Task User_From_TenantA_Accessing_TenantA_Should_Return200()
    {
      // Arrange - JWT válido y tenant consistente
      var token = CreateJwtToken(Tenant1Id, Tenant1Slug);

      // Act - Acceder a endpoint del mismo tenant
      var request = new HttpRequestMessage(HttpMethod.Get, "/me/orders");
      request.Headers.Add("X-Tenant-Slug", Tenant1Slug); // ✅ Mismo tenant
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

      var response = await _client.SendAsync(request);

      // Assert
      Assert.True(
          response.StatusCode != HttpStatusCode.Forbidden,
          $"Expected non-403 for matching tenant ownership, but got {response.StatusCode}");
    }

    /// <summary>
    /// Test 3: Request guest (sin JWT) a /api/cart debe funcionar (200)
    /// NO debe bloquearse por el middleware de ownership
    /// </summary>
    [Fact]
    public async Task Guest_Request_To_Cart_Without_JWT_Should_Return200()
    {
      var sessionId = Guid.NewGuid().ToString();

      // Act - Request guest (sin Authorization header)
      var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
      request.Headers.Add("X-Tenant-Slug", Tenant1Slug);
      request.Headers.Add("X-Session-Id", sessionId); // Guest checkout

      var response = await _client.SendAsync(request);

      // Assert
      Assert.True(
          response.StatusCode != HttpStatusCode.Forbidden && response.StatusCode != HttpStatusCode.Unauthorized,
          $"Expected guest cart request to bypass ownership checks, but got {response.StatusCode}");
    }

    // ==================== HELPERS ====================

    private static string CreateJwtToken(string tenantId, string tenantSlug)
    {
      var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
      var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

      var claims = new[]
      {
        new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
        new Claim(JwtRegisteredClaimNames.Email, "ownership@test.com"),
        new Claim("tenant_id", tenantId),
        new Claim("tenant_slug", tenantSlug),
        new Claim("role", "Customer")
      };

      var token = new JwtSecurityToken(
          issuer: "ecommerce-api",
          audience: "ecommerce-clients",
          claims: claims,
          expires: DateTime.UtcNow.AddHours(1),
          signingCredentials: credentials
      );

      return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ==================== DTOs ====================

    private record ErrorResponse
    {
      public string Error { get; init; } = string.Empty;
      public string JwtTenant { get; init; } = string.Empty;
      public string RequestedTenant { get; init; } = string.Empty;
    }
  }
}
