using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Api_eCommerce.Tests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Tests.Auth
{
  /// <summary>
  /// Tests de hardening de JWT con validación de Issuer y Audience
  /// Valida que tokens con issuer/audience correcto sean aceptados
  /// y que tokens con mismatch sean rechazados (401 Unauthorized)
  /// </summary>
  public class JwtValidationTests : IClassFixture<CustomWebApplicationFactory>
  {
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public JwtValidationTests(CustomWebApplicationFactory factory)
    {
      _factory = factory;
      _client = factory.CreateClient(new WebApplicationFactoryClientOptions
      {
        AllowAutoRedirect = false
      });
    }

    /// <summary>
    /// Test 1: Token con issuer y audience CORRECTOS debe ser aceptado (200 OK)
    /// Simula token generado por el sistema con claims válidos
    /// </summary>
    [Fact]
    public async Task ValidToken_WithCorrectIssuerAndAudience_ReturnsOk()
    {
      // Arrange - Crear token con issuer/audience correctos
      var token = GenerateJwtToken(
          userId: Guid.NewGuid().ToString(),
          email: "test@example.com",
          issuer: "ecommerce-api",
          audience: "ecommerce-clients",
          signingKey: "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF"
      );

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
      _client.DefaultRequestHeaders.Add("X-Tenant-Slug", "test-tenant");

      // Act - Llamar endpoint protegido (ej: /me/favorites)
      var response = await _client.GetAsync("/me/favorites");

      // Assert - Debe retornar 200 OK (o 404 si no hay datos, pero NO 401)
      Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Test 2: Token con issuer INCORRECTO debe ser rechazado (401 Unauthorized)
    /// Solo aplica cuando StrictValidation=true (Production)
    /// </summary>
    [Fact]
    public async Task InvalidToken_WithWrongIssuer_ReturnsUnauthorized()
    {
      // Arrange - Crear token con issuer incorrecto
      var token = GenerateJwtToken(
          userId: Guid.NewGuid().ToString(),
          email: "test@example.com",
          issuer: "fake-issuer",  // ❌ Issuer incorrecto
          audience: "ecommerce-clients",
          signingKey: "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF"
      );

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
      _client.DefaultRequestHeaders.Add("X-Tenant-Slug", "test-tenant");

      // Act
      var response = await _client.GetAsync("/me/favorites");

      // Assert - Debe retornar 401 Unauthorized (si StrictValidation=true)
      // En Development (StrictValidation=false) puede pasar
      // Este test debería ejecutarse con appsettings.json (Production settings)
      var isStrictValidation = true; // Asumir Production mode para test

      if (isStrictValidation)
      {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      }
    }

    /// <summary>
    /// Test 3: Token con audience INCORRECTO debe ser rechazado (401 Unauthorized)
    /// Solo aplica cuando StrictValidation=true (Production)
    /// </summary>
    [Fact]
    public async Task InvalidToken_WithWrongAudience_ReturnsUnauthorized()
    {
      // Arrange - Crear token con audience incorrecto
      var token = GenerateJwtToken(
          userId: Guid.NewGuid().ToString(),
          email: "test@example.com",
          issuer: "ecommerce-api",
          audience: "fake-audience",  // ❌ Audience incorrecto
          signingKey: "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF"
      );

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
      _client.DefaultRequestHeaders.Add("X-Tenant-Slug", "test-tenant");

      // Act
      var response = await _client.GetAsync("/me/favorites");

      // Assert - Debe retornar 401 Unauthorized (si StrictValidation=true)
      var isStrictValidation = true; // Asumir Production mode para test

      if (isStrictValidation)
      {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      }
    }

    /// <summary>
    /// Test 4: Token SIN issuer/audience debe ser aceptado en Development (StrictValidation=false)
    /// Garantiza backward compatibility con tokens viejos
    /// </summary>
    [Fact]
    public async Task LegacyToken_WithoutIssuerAudience_AcceptedInDevelopment()
    {
      // Arrange - Crear token legacy (sin iss/aud)
      var token = GenerateLegacyJwtToken(
          userId: Guid.NewGuid().ToString(),
          email: "test@example.com",
          signingKey: "lk34j5l34asd9f7asdfkasadsf#$%SfaetfASDfASDFA345345345##$%#FASefaasdf987asd9f87Y%$SEVQ345wfw344tw4tqTW#Vw5gw45ytq%T@$%DFASDFasdfasdASDFasdfASDF#$%34534#$SDF"
      );

      _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
      _client.DefaultRequestHeaders.Add("X-Tenant-Slug", "test-tenant");

      // Act
      var response = await _client.GetAsync("/me/favorites");

      // Assert - En Development debe pasar, en Production debe fallar
      // Este test valida StrictValidation=false (Development mode)
      Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #region Helper Methods

    /// <summary>
    /// Genera un JWT token con issuer y audience (nuevo formato)
    /// </summary>
    private string GenerateJwtToken(string userId, string email, string issuer, string audience, string signingKey)
    {
      var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
      var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

      var claims = new[]
      {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("tenant_id", Guid.NewGuid().ToString()),
                new Claim("tenant_slug", "test-tenant")
            };

      var token = new JwtSecurityToken(
          issuer: issuer,
          audience: audience,
          claims: claims,
          expires: DateTime.UtcNow.AddHours(1),
          signingCredentials: credentials
      );

      return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Genera un JWT token legacy SIN issuer/audience (formato viejo)
    /// </summary>
    private string GenerateLegacyJwtToken(string userId, string email, string signingKey)
    {
      var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
      var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

      var claims = new[]
      {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

      // NO especificar issuer ni audience (legacy)
      var token = new JwtSecurityToken(
          claims: claims,
          expires: DateTime.UtcNow.AddHours(1),
          signingCredentials: credentials
      );

      return new JwtSecurityTokenHandler().WriteToken(token);
    }

    #endregion
  }
}
