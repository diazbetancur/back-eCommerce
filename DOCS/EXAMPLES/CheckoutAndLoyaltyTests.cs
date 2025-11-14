using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api_eCommerce.IntegrationTests.E2E
{
    /// <summary>
    /// Tests end-to-end de checkout y sistema de loyalty (puntos de fidelización)
    /// </summary>
    public class CheckoutAndLoyaltyTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;
        private const string TestTenantSlug = "test-loyalty-tenant";

        public CheckoutAndLoyaltyTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // Setup: Always use test tenant
            _client.DefaultRequestHeaders.Add("X-Tenant-Slug", TestTenantSlug);
        }

        [Fact]
        public async Task CompleteCheckoutWithLoyalty_ShouldEarnPoints()
        {
            // ==================== SETUP: AUTH ====================
            var (token, userId) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);

            // ==================== SETUP: CART ====================
            var sessionId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);

            // Get products
            var productsResponse = await _client.GetAsync("/api/catalog/products?pageSize=2");
            var products = await productsResponse.Content.ReadFromJsonAsync<ProductListResponse>();
            var product1 = products!.Items[0];
            var product2 = products.Items[1];

            // ==================== STEP 1: ADD TO CART ====================
            // Act: Add first product
            var addToCart1 = new { productId = product1.Id, quantity = 2 };
            var addToCart1Response = await _client.PostAsJsonAsync("/api/cart/items", addToCart1);

            // Assert: Should add successfully
            addToCart1Response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act: Add second product
            var addToCart2 = new { productId = product2.Id, quantity = 1 };
            var addToCart2Response = await _client.PostAsJsonAsync("/api/cart/items", addToCart2);

            // Assert: Should add successfully
            addToCart2Response.StatusCode.Should().Be(HttpStatusCode.OK);

            // ==================== STEP 2: GET CART ====================
            // Act: Get cart
            var cartResponse = await _client.GetAsync("/api/cart");

            // Assert: Should have 2 items
            cartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var cart = await cartResponse.Content.ReadFromJsonAsync<CartDto>();
            cart.Should().NotBeNull();
            cart!.Items.Should().HaveCount(2);
            cart.ItemCount.Should().Be(3); // 2 + 1
            cart.Subtotal.Should().BeGreaterThan(0);

            // ==================== STEP 3: GET QUOTE ====================
            // Act: Get checkout quote
            var quoteResponse = await _client.PostAsJsonAsync("/api/checkout/quote", new { });

            // Assert: Should calculate totals
            quoteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var quote = await quoteResponse.Content.ReadFromJsonAsync<CheckoutQuoteResponse>();
            quote.Should().NotBeNull();
            quote!.Subtotal.Should().Be(cart.Subtotal);
            quote.Tax.Should().BeGreaterThan(0);
            quote.Shipping.Should().BeGreaterThanOrEqualTo(0);
            quote.Total.Should().Be(quote.Subtotal + quote.Tax + quote.Shipping);

            // ==================== STEP 4: PLACE ORDER ====================
            // Act: Place order with authentication (should earn loyalty points)
            var idempotencyKey = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

            var placeOrderRequest = new
            {
                idempotencyKey,
                email = "test@example.com",
                phone = "+1234567890",
                shippingAddress = "123 Main St, Test City, TC 12345, Country",
                paymentMethod = "CARD"
            };

            var orderResponse = await _client.PostAsJsonAsync("/api/checkout/place-order", placeOrderRequest);

            // Assert: Should create order and earn loyalty points
            orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var order = await orderResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
            order.Should().NotBeNull();
            order!.OrderId.Should().NotBeEmpty();
            order.OrderNumber.Should().NotBeNullOrEmpty();
            order.OrderNumber.Should().StartWith("ORD-");
            order.Status.Should().Be("PENDING");
            order.Total.Should().Be(quote.Total);
            order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // ? CRITICAL: Should earn loyalty points
            order.LoyaltyPointsEarned.Should().NotBeNull();
            order.LoyaltyPointsEarned.Should().BeGreaterThan(0);

            var pointsEarned = order.LoyaltyPointsEarned!.Value;

            // ==================== STEP 5: VERIFY LOYALTY BALANCE ====================
            // Act: Get loyalty account
            var loyaltyResponse = await _client.GetAsync("/me/loyalty");

            // Assert: Balance should match points earned
            loyaltyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var loyalty = await loyaltyResponse.Content.ReadFromJsonAsync<LoyaltyAccountSummaryDto>();
            loyalty.Should().NotBeNull();
            loyalty!.Balance.Should().Be(pointsEarned);
            loyalty.TotalEarned.Should().Be(pointsEarned);
            loyalty.TotalRedeemed.Should().Be(0);
            
            // Should have at least 1 transaction
            loyalty.LastTransactions.Should().NotBeEmpty();
            loyalty.LastTransactions[0].Type.Should().Be("EARN");
            loyalty.LastTransactions[0].Points.Should().Be(pointsEarned);
            loyalty.LastTransactions[0].OrderNumber.Should().Be(order.OrderNumber);

            // ==================== STEP 6: GET LOYALTY TRANSACTIONS ====================
            // Act: Get all loyalty transactions
            var transactionsResponse = await _client.GetAsync("/me/loyalty/transactions?page=1&pageSize=20");

            // Assert: Should have 1 transaction
            transactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var transactions = await transactionsResponse.Content.ReadFromJsonAsync<PagedLoyaltyTransactionsResponse>();
            transactions.Should().NotBeNull();
            transactions!.Items.Should().HaveCount(1);
            transactions.TotalCount.Should().Be(1);
            transactions.Items[0].Type.Should().Be("EARN");
            transactions.Items[0].Points.Should().Be(pointsEarned);

            // ==================== STEP 7: GET ORDER HISTORY ====================
            // Act: Get user orders
            var ordersResponse = await _client.GetAsync("/me/orders?page=1&pageSize=20");

            // Assert: Should have 1 order
            ordersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var orders = await ordersResponse.Content.ReadFromJsonAsync<PagedOrdersResponse>();
            orders.Should().NotBeNull();
            orders!.Items.Should().HaveCount(1);
            orders.Items[0].Id.Should().Be(order.OrderId);
            orders.Items[0].OrderNumber.Should().Be(order.OrderNumber);

            // ==================== STEP 8: GET ORDER DETAILS ====================
            // Act: Get specific order details
            var orderDetailsResponse = await _client.GetAsync($"/me/orders/{order.OrderId}");

            // Assert: Should return full order details
            orderDetailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var orderDetails = await orderDetailsResponse.Content.ReadFromJsonAsync<OrderDetailDto>();
            orderDetails.Should().NotBeNull();
            orderDetails!.Id.Should().Be(order.OrderId);
            orderDetails.Items.Should().HaveCount(2);
            orderDetails.Total.Should().Be(order.Total);
            orderDetails.Status.Should().Be("PENDING");
        }

        [Fact]
        public async Task GuestCheckout_ShouldNotEarnLoyaltyPoints()
        {
            // ==================== SETUP: CART (NO AUTH) ====================
            var sessionId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);

            // Get product
            var productsResponse = await _client.GetAsync("/api/catalog/products?pageSize=1");
            var products = await productsResponse.Content.ReadFromJsonAsync<ProductListResponse>();
            var product = products!.Items[0];

            // ==================== STEP 1: ADD TO CART ====================
            var addToCart = new { productId = product.Id, quantity = 1 };
            var addToCartResponse = await _client.PostAsJsonAsync("/api/cart/items", addToCart);
            addToCartResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // ==================== STEP 2: PLACE ORDER (GUEST) ====================
            var idempotencyKey = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

            var placeOrderRequest = new
            {
                idempotencyKey,
                email = "guest@example.com",
                phone = "+1234567890",
                shippingAddress = "123 Guest St",
                paymentMethod = "CARD"
            };

            var orderResponse = await _client.PostAsJsonAsync("/api/checkout/place-order", placeOrderRequest);

            // Assert: Should create order but NOT earn points
            orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var order = await orderResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
            order.Should().NotBeNull();
            
            // ? CRITICAL: Guest should NOT earn loyalty points
            order!.LoyaltyPointsEarned.Should().BeNull();
        }

        [Fact]
        public async Task PlaceOrder_IdempotencyCheck_ShouldReturnSameOrder()
        {
            // Arrange: Setup authenticated user with cart
            var (token, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);

            var sessionId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);

            await AddProductToCart(sessionId);

            // Act: Place order with idempotency key
            var idempotencyKey = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

            var placeOrderRequest = new
            {
                idempotencyKey,
                email = "test@example.com",
                phone = "+1234567890",
                shippingAddress = "123 Test St",
                paymentMethod = "CARD"
            };

            var firstResponse = await _client.PostAsJsonAsync("/api/checkout/place-order", placeOrderRequest);
            var firstOrder = await firstResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();

            // Act: Try to place order again with same idempotency key
            // (Need to reset session and add products again for this to work)
            var secondSessionId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Remove("X-Session-Id");
            _client.DefaultRequestHeaders.Add("X-Session-Id", secondSessionId);
            await AddProductToCart(secondSessionId);

            var secondResponse = await _client.PostAsJsonAsync("/api/checkout/place-order", placeOrderRequest);

            // Assert: Should return the SAME order (idempotent)
            secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var secondOrder = await secondResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
            secondOrder!.OrderId.Should().Be(firstOrder!.OrderId);
            secondOrder.OrderNumber.Should().Be(firstOrder.OrderNumber);
        }

        [Fact]
        public async Task PlaceOrder_EmptyCart_ShouldFail()
        {
            // Arrange: Setup authenticated user WITHOUT cart
            var (token, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);

            var sessionId = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);

            // Act: Try to place order with empty cart
            var idempotencyKey = Guid.NewGuid().ToString();
            _client.DefaultRequestHeaders.Add("Idempotency-Key", idempotencyKey);

            var placeOrderRequest = new
            {
                idempotencyKey,
                email = "test@example.com",
                phone = "+1234567890",
                shippingAddress = "123 Test St",
                paymentMethod = "CARD"
            };

            var response = await _client.PostAsJsonAsync("/api/checkout/place-order", placeOrderRequest);

            // Assert: Should return 400 Bad Request
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // ==================== HELPER METHODS ====================

        private async Task<(string Token, Guid UserId)> RegisterAndLoginAsync()
        {
            var email = $"test.{Guid.NewGuid():N}@example.com";
            var registerRequest = new
            {
                email,
                password = "TestPass123!",
                firstName = "Test",
                lastName = "User"
            };

            var response = await _client.PostAsJsonAsync("/auth/register", registerRequest);
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

            return (result!.Token, result.User.Id);
        }

        private async Task AddProductToCart(string sessionId)
        {
            var productsResponse = await _client.GetAsync("/api/catalog/products?pageSize=1");
            var products = await productsResponse.Content.ReadFromJsonAsync<ProductListResponse>();
            var product = products!.Items[0];

            var addToCart = new { productId = product.Id, quantity = 1 };
            await _client.PostAsJsonAsync("/api/cart/items", addToCart);
        }

        // ==================== DTOs ====================

        private record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
        private record UserDto(Guid Id, string Email, string FirstName, string LastName);

        private record ProductListResponse(List<ProductDto> Items, int TotalCount);
        private record ProductDto(Guid Id, string Name, decimal Price, int Stock, bool IsActive);

        private record CartDto(
            Guid Id,
            string SessionId,
            List<CartItemDto> Items,
            int ItemCount,
            decimal Subtotal
        );

        private record CartItemDto(
            Guid Id,
            Guid ProductId,
            string ProductName,
            decimal Price,
            int Quantity,
            decimal Subtotal
        );

        private record CheckoutQuoteResponse(
            decimal Subtotal,
            decimal Tax,
            decimal Shipping,
            decimal Total,
            List<CartItemDto> Items
        );

        private record PlaceOrderResponse(
            Guid OrderId,
            string OrderNumber,
            decimal Total,
            string Status,
            DateTime CreatedAt,
            int? LoyaltyPointsEarned
        );

        private record LoyaltyAccountSummaryDto(
            int Balance,
            int TotalEarned,
            int TotalRedeemed,
            List<LoyaltyTransactionDto> LastTransactions
        );

        private record LoyaltyTransactionDto(
            Guid Id,
            string Type,
            int Points,
            string? Description,
            string? OrderNumber,
            DateTime CreatedAt
        );

        private record PagedLoyaltyTransactionsResponse(
            List<LoyaltyTransactionDto> Items,
            int TotalCount,
            int Page,
            int PageSize,
            int TotalPages
        );

        private record PagedOrdersResponse(
            List<OrderSummaryDto> Items,
            int TotalCount,
            int Page,
            int PageSize,
            int TotalPages
        );

        private record OrderSummaryDto(
            Guid Id,
            string OrderNumber,
            string Status,
            decimal Total,
            DateTime CreatedAt,
            int ItemCount
        );

        private record OrderDetailDto(
            Guid Id,
            string OrderNumber,
            string Status,
            decimal Total,
            decimal Subtotal,
            decimal Tax,
            decimal Shipping,
            string ShippingAddress,
            string Email,
            string? Phone,
            string PaymentMethod,
            DateTime CreatedAt,
            DateTime? CompletedAt,
            List<OrderItemDetailDto> Items
        );

        private record OrderItemDetailDto(
            Guid Id,
            Guid ProductId,
            string ProductName,
            int Quantity,
            decimal Price,
            decimal Subtotal
        );
    }
}
