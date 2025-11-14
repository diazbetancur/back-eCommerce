# ?? Integration Tests Guide - E2E Testing

## Overview

Este proyecto contiene tests de integración end-to-end para validar el flujo completo de la aplicación eCommerce multi-tenant.

---

## ?? Estructura del Proyecto

```
Api-eCommerce.IntegrationTests/
??? Api-eCommerce.IntegrationTests.csproj
??? TestWebApplicationFactory.cs           ? Factory para tests in-memory
??? E2E/
?   ??? ProvisioningTests.cs              ? Tests de provisioning de tenant
?   ??? AuthAndFavoritesTests.cs          ? Tests de auth + favoritos
?   ??? CheckoutAndLoyaltyTests.cs        ? Tests de checkout + loyalty
??? Helpers/
    ??? TestDataGenerator.cs              ? Generación de datos de prueba
    ??? HttpClientExtensions.cs           ? Extensiones para HTTP client
```

---

## ?? Cómo Ejecutar los Tests

### Opción 1: Visual Studio
1. Abrir Test Explorer (`Test > Test Explorer`)
2. Click en "Run All Tests"

### Opción 2: CLI
```bash
# Ejecutar todos los tests
dotnet test

# Ejecutar tests de una clase específica
dotnet test --filter "FullyQualifiedName~ProvisioningTests"

# Ejecutar con verbosity detallada
dotnet test --logger "console;verbosity=detailed"

# Ejecutar con coverage
dotnet test /p:CollectCoverage=true
```

---

## ??? TestWebApplicationFactory

Factory personalizada que configura la aplicación en memoria para tests.

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remover DbContext real
            RemoveDbContext<AdminDbContext>(services);
            RemoveDbContext<TenantDbContext>(services);
            
            // Agregar DbContext in-memory
            services.AddDbContext<AdminDbContext>(options =>
                options.UseInMemoryDatabase("TestAdminDb"));
            
            // Seed data para tests
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            SeedTestData(scope.ServiceProvider);
        });
    }
}
```

---

## ?? Tests Implementados

### 1. ProvisioningTests

Valida el flujo completo de provisioning de tenant.

```csharp
[Fact]
public async Task CompleteProvisioningFlow_ShouldSucceed()
{
    // Arrange
    var initRequest = new { slug = "test-store", name = "Test", plan = "Premium" };
    
    // Act: Initialize
    var initResponse = await _client.PostAsJsonAsync("/provision/tenants/init", initRequest);
    
    // Assert: Should return confirm token
    initResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var initResult = await initResponse.Content.ReadFromJsonAsync<InitResponse>();
    initResult.ConfirmToken.Should().NotBeNullOrEmpty();
    
    // Act: Confirm
    _client.SetBearerToken(initResult.ConfirmToken);
    var confirmResponse = await _client.PostAsync("/provision/tenants/confirm", null);
    
    // Assert: Should queue provisioning
    confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // Act: Check status
    var statusResponse = await _client.GetAsync($"/provision/tenants/{initResult.ProvisioningId}/status");
    
    // Assert: Should be ready
    var status = await statusResponse.Content.ReadFromJsonAsync<StatusResponse>();
    status.Status.Should().Be("Ready");
}
```

### 2. AuthAndFavoritesTests

Valida autenticación de usuario y gestión de favoritos.

```csharp
[Fact]
public async Task RegisterLoginAndManageFavorites_ShouldSucceed()
{
    // Arrange
    _client.DefaultRequestHeaders.Add("X-Tenant-Slug", "test-tenant");
    var registerRequest = new 
    { 
        email = "test@example.com", 
        password = "TestPass123!",
        firstName = "Test",
        lastName = "User"
    };
    
    // Act: Register
    var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
    
    // Assert: Should return token
    registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
    authResult.Token.Should().NotBeNullOrEmpty();
    
    // Act: Login
    var loginRequest = new { email = registerRequest.email, password = registerRequest.password };
    var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);
    var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
    
    // Set JWT token
    _client.SetBearerToken(loginResult.Token);
    
    // Act: Add to favorites
    var productId = Guid.NewGuid();
    var addFavoriteResponse = await _client.PostAsJsonAsync("/me/favorites", new { productId });
    
    // Assert: Should add successfully
    addFavoriteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // Act: Get favorites
    var favoritesResponse = await _client.GetAsync("/me/favorites");
    
    // Assert: Should return list with 1 item
    var favorites = await favoritesResponse.Content.ReadFromJsonAsync<FavoritesResponse>();
    favorites.Items.Should().HaveCount(1);
    favorites.Items[0].ProductId.Should().Be(productId);
}
```

### 3. CheckoutAndLoyaltyTests

Valida flujo completo de checkout y acumulación de puntos de loyalty.

```csharp
[Fact]
public async Task CompleteCheckoutWithLoyalty_ShouldEarnPoints()
{
    // Arrange
    await SetupAuthenticatedUser();
    var sessionId = Guid.NewGuid().ToString();
    var productId = await SeedProduct();
    
    _client.DefaultRequestHeaders.Add("X-Tenant-Slug", "test-tenant");
    _client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);
    
    // Act: Add to cart
    var addToCartResponse = await _client.PostAsJsonAsync("/api/cart/items", 
        new { productId, quantity = 2 });
    
    // Assert: Cart should have items
    addToCartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    
    // Act: Place order
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
    
    var orderResponse = await _client.PostAsJsonAsync("/api/checkout/place-order", placeOrderRequest);
    
    // Assert: Should create order and earn points
    orderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var order = await orderResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>();
    order.LoyaltyPointsEarned.Should().BeGreaterThan(0);
    
    // Act: Check loyalty balance
    var loyaltyResponse = await _client.GetAsync("/me/loyalty");
    
    // Assert: Balance should match points earned
    var loyalty = await loyaltyResponse.Content.ReadFromJsonAsync<LoyaltyResponse>();
    loyalty.Balance.Should().Be(order.LoyaltyPointsEarned);
    loyalty.TotalEarned.Should().Be(order.LoyaltyPointsEarned);
    
    // Act: Check order history
    var ordersResponse = await _client.GetAsync("/me/orders");
    
    // Assert: Should have 1 order
    var orders = await ordersResponse.Content.ReadFromJsonAsync<OrdersResponse>();
    orders.Items.Should().HaveCount(1);
    orders.Items[0].Id.Should().Be(order.OrderId);
}
```

---

## ??? Helper Methods

### HttpClientExtensions

```csharp
public static class HttpClientExtensions
{
    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }
    
    public static void SetTenantSlug(this HttpClient client, string slug)
    {
        client.DefaultRequestHeaders.Add("X-Tenant-Slug", slug);
    }
    
    public static void SetSessionId(this HttpClient client, string sessionId)
    {
        client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);
    }
}
```

### TestDataGenerator

```csharp
public static class TestDataGenerator
{
    public static Tenant CreateTestTenant(string slug = "test-tenant")
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = "Test Tenant",
            Status = TenantStatus.Ready,
            PlanId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public static Product CreateTestProduct(decimal price = 100m)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Description = "Test Description",
            Price = price,
            Stock = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
```

---

## ?? Asserts Comunes

### FluentAssertions Examples

```csharp
// Status Code
response.StatusCode.Should().Be(HttpStatusCode.OK);
response.StatusCode.Should().Be(HttpStatusCode.Created);
response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

// Objects
result.Token.Should().NotBeNullOrEmpty();
result.User.Id.Should().NotBeEmpty();
result.Total.Should().BeGreaterThan(0);

// Collections
items.Should().HaveCount(5);
items.Should().Contain(x => x.Id == expectedId);
items.Should().AllSatisfy(x => x.IsActive.Should().BeTrue());

// Dates
order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

// Exceptions
Func<Task> act = async () => await _service.GetAsync(invalidId);
await act.Should().ThrowAsync<InvalidOperationException>()
    .WithMessage("*not found*");
```

---

## ?? Test Scenarios

### Scenario 1: Happy Path - Complete Flow

```csharp
[Fact]
public async Task CompleteHappyPath_ShouldSucceed()
{
    // 1. Provision tenant
    var tenant = await ProvisionTenantAsync("happy-path-tenant");
    
    // 2. Register and login user
    var (token, userId) = await RegisterAndLoginAsync(tenant.Slug);
    
    // 3. Browse catalog and add to favorites
    var product = await GetFirstProductAsync(tenant.Slug);
    await AddToFavoritesAsync(token, product.Id);
    
    // 4. Add to cart
    var sessionId = Guid.NewGuid().ToString();
    await AddToCartAsync(tenant.Slug, sessionId, product.Id, quantity: 2);
    
    // 5. Checkout
    var order = await PlaceOrderAsync(tenant.Slug, sessionId, token);
    
    // 6. Verify
    order.Status.Should().Be("PENDING");
    order.LoyaltyPointsEarned.Should().BeGreaterThan(0);
    
    // 7. Check loyalty
    var loyalty = await GetLoyaltyAsync(token);
    loyalty.Balance.Should().Be(order.LoyaltyPointsEarned);
}
```

### Scenario 2: Guest Checkout (No Loyalty)

```csharp
[Fact]
public async Task GuestCheckout_ShouldNotEarnPoints()
{
    // Arrange
    var tenant = await ProvisionTenantAsync("guest-tenant");
    var product = await GetFirstProductAsync(tenant.Slug);
    var sessionId = Guid.NewGuid().ToString();
    
    // Act: Add to cart (no auth)
    await AddToCartAsync(tenant.Slug, sessionId, product.Id, quantity: 1);
    
    // Act: Checkout as guest (no auth token)
    var order = await PlaceOrderAsync(tenant.Slug, sessionId, token: null);
    
    // Assert: No loyalty points for guest
    order.LoyaltyPointsEarned.Should().BeNull();
}
```

### Scenario 3: Idempotency Check

```csharp
[Fact]
public async Task PlaceOrderTwiceWithSameKey_ShouldReturnSameOrder()
{
    // Arrange
    var (token, sessionId) = await SetupUserAndCart();
    var idempotencyKey = Guid.NewGuid().ToString();
    
    // Act: Place order first time
    var order1 = await PlaceOrderAsync(sessionId, token, idempotencyKey);
    
    // Act: Place order again with same key
    var order2 = await PlaceOrderAsync(sessionId, token, idempotencyKey);
    
    // Assert: Should return same order
    order2.OrderId.Should().Be(order1.OrderId);
    order2.OrderNumber.Should().Be(order1.OrderNumber);
}
```

---

## ?? Debugging Tips

### 1. Ver Requests/Responses Completos

```csharp
[Fact]
public async Task DebugTest()
{
    var response = await _client.GetAsync("/api/catalog/products");
    
    // Log request
    Console.WriteLine($"Request: {response.RequestMessage.Method} {response.RequestMessage.RequestUri}");
    
    // Log response
    Console.WriteLine($"Status: {response.StatusCode}");
    var content = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"Content: {content}");
}
```

### 2. Breakpoints en Tests

- Coloca breakpoints en las líneas de Assert
- Inspecciona el objeto `response` en el Watch window
- Verifica `response.Content.ReadAsStringAsync()` para ver el body

### 3. Test Explorer Output

- Click derecho en el test ? "Open test output"
- Verifica los logs de la aplicación

---

## ? Checklist de Tests

### Provisioning
- [x] Initialize tenant
- [x] Confirm provisioning
- [x] Check status until ready
- [ ] Handle failed provisioning
- [ ] Handle duplicate slug

### Authentication
- [x] Register new user
- [x] Login with valid credentials
- [x] Get user profile
- [ ] Login with invalid credentials
- [ ] Register with existing email

### Favorites
- [x] Add product to favorites
- [x] Get favorites list
- [x] Check if product is favorite
- [x] Remove from favorites
- [ ] Add same product twice (idempotent)
- [ ] Add inactive product (should fail)

### Cart & Checkout
- [x] Add item to cart
- [x] Get cart
- [x] Update cart item quantity
- [x] Remove cart item
- [x] Get checkout quote
- [x] Place order (authenticated)
- [ ] Place order (guest)
- [ ] Place order with insufficient stock
- [ ] Place order with idempotency key

### Loyalty
- [x] Earn points on order
- [x] Check loyalty balance
- [x] Get loyalty transactions
- [ ] Filter transactions by type
- [ ] Guest order (no points)

### Orders
- [x] Get order history
- [x] Get order details
- [ ] Filter by status
- [ ] Filter by date range
- [ ] Access other user's order (should fail)

---

## ?? Referencias

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [WebApplicationFactory Documentation](https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [InMemoryDatabase Documentation](https://docs.microsoft.com/en-us/ef/core/providers/in-memory/)

---

## ?? Próximos Pasos

1. **Agregar más escenarios edge case:**
   - Concurrent orders
   - Race conditions
   - Timeout scenarios

2. **Performance testing:**
   - Load tests
   - Stress tests
   - Endurance tests

3. **Test data builders:**
   - Fluent API para crear datos de prueba
   - Faker para datos realistas

4. **CI/CD Integration:**
   - GitHub Actions workflow
   - Azure DevOps pipeline

---

**Última actualización:** Diciembre 2024  
**Versión:** 1.0
