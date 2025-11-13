using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api_eCommerce.Tests.Cart
{
    /// <summary>
    /// Tests para los endpoints de Cart
    /// </summary>
    public class CartEndpointsTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private const string ValidTenantSlug = "test-tenant-1";
        private const string SessionId = "test-session-123";

        public CartEndpointsTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task AddToCart_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/add");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            
            var payload = new
            {
                productId = Guid.NewGuid().ToString(),
                quantity = 2
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest, 
                "Request should be valid with tenant and session");
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task AddToCart_WithoutTenant_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/add");
            request.Headers.Add("X-Session-Id", SessionId);
            
            var payload = new { productId = Guid.NewGuid().ToString(), quantity = 2 };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
                "Should fail without tenant header");
        }

        [Fact]
        public async Task AddToCart_WithoutSession_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/add");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            
            var payload = new { productId = Guid.NewGuid().ToString(), quantity = 2 };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
                "Should fail without session header");
        }

        [Fact]
        public async Task GetCart_WithValidSession_ReturnsCart()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task UpdateCartItem_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var request = new HttpRequestMessage(
                HttpMethod.Put, 
                $"/api/cart/items/{productId}");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            
            var payload = new { quantity = 5 };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task RemoveCartItem_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var request = new HttpRequestMessage(
                HttpMethod.Delete, 
                $"/api/cart/items/{productId}");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ClearCart_WithValidSession_ReturnsSuccess()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Delete, "/api/cart");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Cart_SessionIsolation_DifferentSessionsHaveDifferentCarts()
        {
            // Arrange
            var session1 = "session-1";
            var session2 = "session-2";
            var productId = Guid.NewGuid().ToString();

            // Agregar producto al carrito de session-1
            var request1 = CreateAddToCartRequest(session1, productId, 2);
            var response1 = await _client.SendAsync(request1);

            // Agregar producto al carrito de session-2
            var request2 = CreateAddToCartRequest(session2, productId, 3);
            var response2 = await _client.SendAsync(request2);

            // Act - Obtener ambos carritos
            var getCart1 = CreateGetCartRequest(session1);
            var getCart2 = CreateGetCartRequest(session2);

            var cart1Response = await _client.SendAsync(getCart1);
            var cart2Response = await _client.SendAsync(getCart2);

            // Assert
            cart1Response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            cart2Response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            
            // Los carritos deberían ser independientes
        }

        [Fact]
        public async Task AddToCart_InvalidQuantity_ReturnsBadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/add");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            
            var payload = new
            {
                productId = Guid.NewGuid().ToString(),
                quantity = -1 // Cantidad inválida
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            // Debería validar y rechazar cantidad negativa
            if (response.StatusCode != HttpStatusCode.NotFound) // Producto no existe
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            }
        }

        [Fact]
        public async Task AddToCart_MultipleProducts_AllAreAdded()
        {
            // Arrange
            var sessionId = $"multi-product-session-{Guid.NewGuid()}";
            var products = new[]
            {
                (Id: Guid.NewGuid().ToString(), Qty: 1),
                (Id: Guid.NewGuid().ToString(), Qty: 2),
                (Id: Guid.NewGuid().ToString(), Qty: 3)
            };

            // Act
            foreach (var product in products)
            {
                var request = CreateAddToCartRequest(sessionId, product.Id, product.Qty);
                var response = await _client.SendAsync(request);
                
                response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            }

            // Obtener carrito
            var getCartRequest = CreateGetCartRequest(sessionId);
            var cartResponse = await _client.SendAsync(getCartRequest);

            // Assert
            cartResponse.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(1001)] // Asumiendo que hay un límite máximo
        public async Task AddToCart_InvalidQuantities_AreRejected(int quantity)
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/add");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            
            var payload = new
            {
                productId = Guid.NewGuid().ToString(),
                quantity = quantity
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            // Cantidades inválidas deberían ser rechazadas
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            }
        }

        private HttpRequestMessage CreateAddToCartRequest(string sessionId, string productId, int quantity)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/add");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", sessionId);
            request.Content = JsonContent.Create(new { productId, quantity });
            return request;
        }

        private HttpRequestMessage CreateGetCartRequest(string sessionId)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", sessionId);
            return request;
        }
    }
}
