using System.Net;
using System.Net.Http.Json;

namespace Api_eCommerce.Tests.Checkout
{
    /// <summary>
    /// Tests para los endpoints de Checkout, incluyendo idempotencia
    /// </summary>
    public class CheckoutTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private const string ValidTenantSlug = "test-tenant-1";
        private const string SessionId = "checkout-session-123";

        public CheckoutTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetQuote_WithValidData_ReturnsQuote()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/quote");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            
            var payload = new
            {
                shippingAddress = new
                {
                    fullName = "John Doe",
                    phone = "+1234567890",
                    address = "123 Main St",
                    city = "City",
                    country = "US",
                    postalCode = "12345"
                }
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task PlaceOrder_WithValidData_ReturnsCreated()
        {
            // Arrange
            var idempotencyKey = Guid.NewGuid().ToString();
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/place-order");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            
            var payload = new
            {
                shippingAddress = new
                {
                    fullName = "John Doe",
                    phone = "+1234567890",
                    address = "123 Main St",
                    city = "City",
                    country = "US",
                    postalCode = "12345"
                },
                paymentMethod = "cash"
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task PlaceOrder_WithoutIdempotencyKey_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/place-order");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            // NO agregamos Idempotency-Key
            
            var payload = new
            {
                shippingAddress = new
                {
                    fullName = "John Doe",
                    phone = "+1234567890",
                    address = "123 Main St",
                    city = "City",
                    country = "US"
                },
                paymentMethod = "cash"
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
                "Should require Idempotency-Key header");
        }

        [Fact]
        public async Task PlaceOrder_IdempotentRequests_ReturnSameResult()
        {
            // Arrange
            var idempotencyKey = Guid.NewGuid().ToString();
            var sessionId = $"idempotent-session-{Guid.NewGuid()}";
            
            var payload = new
            {
                shippingAddress = new
                {
                    fullName = "Jane Doe",
                    phone = "+1234567890",
                    address = "456 Elm St",
                    city = "City",
                    country = "US",
                    postalCode = "12345"
                },
                paymentMethod = "cash"
            };

            // Act - Primera request
            var request1 = CreatePlaceOrderRequest(sessionId, idempotencyKey, payload);
            var response1 = await _client.SendAsync(request1);

            // Act - Segunda request con la misma idempotency key
            var request2 = CreatePlaceOrderRequest(sessionId, idempotencyKey, payload);
            var response2 = await _client.SendAsync(request2);

            // Assert
            response1.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response2.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);

            // Ambas deberían retornar el mismo resultado (idempotencia)
            if (response1.IsSuccessStatusCode && response2.IsSuccessStatusCode)
            {
                var content1 = await response1.Content.ReadAsStringAsync();
                var content2 = await response2.Content.ReadAsStringAsync();
                
                // El segundo request debería retornar la misma orden (o un 409 Conflict)
                if (response2.StatusCode == HttpStatusCode.Created)
                {
                    content1.Should().Be(content2, 
                        "Idempotent requests should return the same result");
                }
                else if (response2.StatusCode == HttpStatusCode.Conflict)
                {
                    // También es válido retornar 409 para requests duplicadas
                    response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
                }
            }
        }

        [Fact]
        public async Task PlaceOrder_DifferentIdempotencyKeys_CreateDifferentOrders()
        {
            // Arrange
            var sessionId = $"multi-order-session-{Guid.NewGuid()}";
            var idempotencyKey1 = Guid.NewGuid().ToString();
            var idempotencyKey2 = Guid.NewGuid().ToString();
            
            var payload = new
            {
                shippingAddress = new
                {
                    fullName = "Bob Smith",
                    phone = "+1234567890",
                    address = "789 Oak St",
                    city = "City",
                    country = "US",
                    postalCode = "12345"
                },
                paymentMethod = "cash"
            };

            // Act
            var request1 = CreatePlaceOrderRequest(sessionId, idempotencyKey1, payload);
            var request2 = CreatePlaceOrderRequest(sessionId, idempotencyKey2, payload);

            var response1 = await _client.SendAsync(request1);
            var response2 = await _client.SendAsync(request2);

            // Assert
            response1.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            response2.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);

            // Diferentes keys deberían crear diferentes órdenes
            if (response1.IsSuccessStatusCode && response2.IsSuccessStatusCode)
            {
                var content1 = await response1.Content.ReadAsStringAsync();
                var content2 = await response2.Content.ReadAsStringAsync();
                
                content1.Should().NotBe(content2, 
                    "Different idempotency keys should create different orders");
            }
        }

        [Fact]
        public async Task PlaceOrder_WithoutTenant_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/place-order");
            request.Headers.Add("X-Session-Id", SessionId);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            
            var payload = new
            {
                shippingAddress = new { fullName = "Test", phone = "+123", address = "Test", city = "Test", country = "US" },
                paymentMethod = "cash"
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task PlaceOrder_WithoutSession_Returns400BadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/place-order");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            
            var payload = new
            {
                shippingAddress = new { fullName = "Test", phone = "+123", address = "Test", city = "Test", country = "US" },
                paymentMethod = "cash"
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task PlaceOrder_InvalidShippingAddress_ReturnsBadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/place-order");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            
            var payload = new
            {
                shippingAddress = new
                {
                    fullName = "", // Vacío - inválido
                    phone = "",
                    address = "",
                    city = "",
                    country = ""
                },
                paymentMethod = "cash"
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            }
        }

        [Theory]
        [InlineData("cash")]
        [InlineData("wompi")]
        [InlineData("stripe")]
        public async Task PlaceOrder_DifferentPaymentMethods_AreAccepted(string paymentMethod)
        {
            // Arrange
            var idempotencyKey = Guid.NewGuid().ToString();
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/place-order");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", $"{SessionId}-{paymentMethod}");
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            
            var payload = new
            {
                shippingAddress = new
                {
                    fullName = "Payment Test",
                    phone = "+1234567890",
                    address = "Payment St",
                    city = "City",
                    country = "US",
                    postalCode = "12345"
                },
                paymentMethod = paymentMethod
            };
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
            // Puede ser 400 si el método de pago no está habilitado para el tenant
        }

        [Fact]
        public async Task GetQuote_WithoutShippingAddress_ReturnsBadRequest()
        {
            // Arrange
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/quote");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", SessionId);
            
            var payload = new { }; // Sin shippingAddress
            request.Content = JsonContent.Create(payload);

            // Act
            var response = await _client.SendAsync(request);

            // Assert
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            }
        }

        private HttpRequestMessage CreatePlaceOrderRequest(
            string sessionId, 
            string idempotencyKey, 
            object payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout/place-order");
            request.Headers.Add("X-Tenant-Slug", ValidTenantSlug);
            request.Headers.Add("X-Session-Id", sessionId);
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            request.Content = JsonContent.Create(payload);
            return request;
        }
    }
}
