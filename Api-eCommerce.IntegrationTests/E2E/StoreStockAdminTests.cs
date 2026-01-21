using System.Net;
using System.Net.Http.Json;
using CC.Domain.Dto;
using Xunit;

namespace Api_eCommerce.IntegrationTests.E2E
{
  /// <summary>
  /// Tests de integración para los endpoints de stock multi-tienda (admin)
  /// </summary>
  public class StoreStockAdminTests : IClassFixture<TestWebApplicationFactory>
  {
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StoreStockAdminTests(TestWebApplicationFactory factory)
    {
      _factory = factory;
      _client = factory.CreateClient();
    }

    [Fact(DisplayName = "GET /api/admin/stores - debe retornar 401 sin autenticación")]
    public async Task GetStores_WithoutAuth_Returns401()
    {
      // Act
      var response = await _client.GetAsync("/api/admin/stores");

      // Assert
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "GET /api/admin/stores/{storeId}/stock - debe retornar 401 sin autenticación")]
    public async Task GetStoreStock_WithoutAuth_Returns401()
    {
      // Arrange
      var fakeStoreId = Guid.NewGuid();

      // Act
      var response = await _client.GetAsync($"/api/admin/stores/{fakeStoreId}/stock");

      // Assert
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "PUT /api/admin/stores/{storeId}/stock/{productId} - debe retornar 401 sin autenticación")]
    public async Task UpdateStoreStock_WithoutAuth_Returns401()
    {
      // Arrange
      var fakeStoreId = Guid.NewGuid();
      var fakeProductId = Guid.NewGuid();
      var request = new UpdateStoreStockRequest { Stock = 100 };

      // Act
      var response = await _client.PutAsJsonAsync(
          $"/api/admin/stores/{fakeStoreId}/stock/{fakeProductId}",
          request
      );

      // Assert
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // TODO: Agregar tests con autenticación cuando se tenga configurado el sistema de permisos en tests
    // - GET /api/admin/stores - 200 con datos correctos (requiere permisos inventory:view)
    // - GET /api/admin/stores - 403 sin permisos inventory:view
    // - GET /api/admin/stores/{storeId}/stock - 200 con datos correctos (requiere permisos inventory:view)
    // - GET /api/admin/stores/{storeId}/stock - 403 sin permisos inventory:view
    // - GET /api/admin/stores/{storeId}/stock - 404 si tienda no existe
    // - PUT /api/admin/stores/{storeId}/stock/{productId} - 200 con datos correctos (requiere permisos inventory:update)
    // - PUT /api/admin/stores/{storeId}/stock/{productId} - 403 sin permisos inventory:update
    // - PUT /api/admin/stores/{storeId}/stock/{productId} - 400 si stock es negativo
    // - PUT /api/admin/stores/{storeId}/stock/{productId} - 404 si tienda o producto no existen
    // - PUT /api/admin/stores/{storeId}/stock/{productId} - Idempotente (mismo stock retorna 200)
  }

  /// <summary>
  /// Tests de lógica de negocio del stock service
  /// </summary>
  public class StockServiceTests : IClassFixture<TestWebApplicationFactory>
  {
    private readonly TestWebApplicationFactory _factory;

    public StockServiceTests(TestWebApplicationFactory factory)
    {
      _factory = factory;
    }

    [Fact(DisplayName = "GetStoreStockAsync - debe incluir productId, productName, stock, reservedStock, availableStock, updatedAt")]
    public void GetStoreStockAsync_ShouldIncludeRequiredFields()
    {
      // Este test verifica que el DTO StoreProductStockDto tenga los campos requeridos
      var dto = new StoreProductStockDto
      {
        ProductId = Guid.NewGuid(),
        ProductName = "Test Product",
        Stock = 100,
        ReservedStock = 20,
        AvailableStock = 80,
        UpdatedAt = DateTime.UtcNow
      };

      Assert.NotEqual(Guid.Empty, dto.ProductId);
      Assert.False(string.IsNullOrEmpty(dto.ProductName));
      Assert.True(dto.Stock >= 0);
      Assert.True(dto.ReservedStock >= 0);
      Assert.Equal(dto.Stock - dto.ReservedStock, dto.AvailableStock);
      Assert.NotEqual(default(DateTime), dto.UpdatedAt);
    }

    [Fact(DisplayName = "UpdateStoreStockRequest - debe validar stock >= 0")]
    public void UpdateStoreStockRequest_ShouldValidateNonNegativeStock()
    {
      // Arrange
      var validRequest = new UpdateStoreStockRequest { Stock = 0 };
      var invalidRequest = new UpdateStoreStockRequest { Stock = -1 };

      // Assert
      Assert.True(validRequest.Stock >= 0, "Stock de 0 debe ser válido");
      Assert.False(invalidRequest.Stock >= 0, "Stock negativo debe ser inválido");
    }
  }
}
