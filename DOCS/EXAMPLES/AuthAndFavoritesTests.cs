using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Api_eCommerce.IntegrationTests.E2E
{
    /// <summary>
    /// Tests end-to-end de autenticación de usuarios y gestión de favoritos
    /// </summary>
    public class AuthAndFavoritesTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;
        private const string TestTenantSlug = "test-auth-tenant";

        public AuthAndFavoritesTests(TestWebApplicationFactory factory)
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
        public async Task RegisterLoginAndManageFavorites_CompleteFlow_ShouldSucceed()
        {
            // ==================== STEP 1: REGISTER ====================
            // Arrange
            var email = $"test.{Guid.NewGuid():N}@example.com";
            var registerRequest = new
            {
                email,
                password = "TestPass123!",
                firstName = "Test",
                lastName = "User",
                phoneNumber = "+1234567890"
            };

            // Act: Register new user
            var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);

            // Assert: Should return 200 with token
            registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var registerResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
            registerResult.Should().NotBeNull();
            registerResult!.Token.Should().NotBeNullOrEmpty();
            registerResult.User.Should().NotBeNull();
            registerResult.User.Email.Should().Be(email);
            registerResult.User.FirstName.Should().Be("Test");
            registerResult.User.LastName.Should().Be("User");
            registerResult.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

            var token = registerResult.Token;
            var userId = registerResult.User.Id;

            // ==================== STEP 2: GET PROFILE ====================
            // Act: Get user profile
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);
            
            var profileResponse = await _client.GetAsync("/auth/me");

            // Assert: Should return user profile
            profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var profile = await profileResponse.Content.ReadFromJsonAsync<UserProfileDto>();
            profile.Should().NotBeNull();
            profile!.Id.Should().Be(userId);
            profile.Email.Should().Be(email);

            // ==================== STEP 3: LOGIN ====================
            // Act: Login with credentials
            var loginRequest = new
            {
                email,
                password = "TestPass123!"
            };

            // Remove token to test login
            _client.DefaultRequestHeaders.Authorization = null;

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);

            // Assert: Should return new token
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            loginResult.Should().NotBeNull();
            loginResult!.Token.Should().NotBeNullOrEmpty();
            loginResult.User.Id.Should().Be(userId);

            // Use new token
            token = loginResult.Token;
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);

            // ==================== STEP 4: ADD TO FAVORITES ====================
            // Arrange: Get a product to favorite
            var productsResponse = await _client.GetAsync("/api/catalog/products?pageSize=1");
            var products = await productsResponse.Content.ReadFromJsonAsync<ProductListResponse>();
            var productId = products!.Items[0].Id;

            // Act: Add product to favorites
            var addFavoriteRequest = new { productId };
            var addFavoriteResponse = await _client.PostAsJsonAsync("/me/favorites", addFavoriteRequest);

            // Assert: Should add successfully
            addFavoriteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var addFavoriteResult = await addFavoriteResponse.Content.ReadFromJsonAsync<AddFavoriteResponse>();
            addFavoriteResult.Should().NotBeNull();
            addFavoriteResult!.ProductId.Should().Be(productId);
            addFavoriteResult.Message.Should().Contain("added");

            // ==================== STEP 5: GET FAVORITES ====================
            // Act: Get favorites list
            var favoritesResponse = await _client.GetAsync("/me/favorites");

            // Assert: Should return list with 1 item
            favoritesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var favorites = await favoritesResponse.Content.ReadFromJsonAsync<FavoriteListResponse>();
            favorites.Should().NotBeNull();
            favorites!.Items.Should().HaveCount(1);
            favorites.Items[0].ProductId.Should().Be(productId);
            favorites.Items[0].IsActive.Should().BeTrue();
            favorites.TotalCount.Should().Be(1);

            // ==================== STEP 6: CHECK IF FAVORITE ====================
            // Act: Check if product is favorite
            var checkResponse = await _client.GetAsync($"/me/favorites/check/{productId}");

            // Assert: Should return true
            checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var checkResult = await checkResponse.Content.ReadFromJsonAsync<CheckFavoriteResponse>();
            checkResult.Should().NotBeNull();
            checkResult!.IsFavorite.Should().BeTrue();

            // ==================== STEP 7: ADD SAME PRODUCT AGAIN (IDEMPOTENT) ====================
            // Act: Try to add same product again
            var addAgainResponse = await _client.PostAsJsonAsync("/me/favorites", addFavoriteRequest);

            // Assert: Should return OK (idempotent)
            addAgainResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var addAgainResult = await addAgainResponse.Content.ReadFromJsonAsync<AddFavoriteResponse>();
            addAgainResult!.Message.Should().Contain("already");

            // ==================== STEP 8: REMOVE FROM FAVORITES ====================
            // Act: Remove from favorites
            var removeResponse = await _client.DeleteAsync($"/me/favorites/{productId}");

            // Assert: Should remove successfully
            removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Act: Get favorites again
            var favoritesAfterRemove = await _client.GetAsync("/me/favorites");
            var favoritesAfterRemoveResult = await favoritesAfterRemove.Content.ReadFromJsonAsync<FavoriteListResponse>();

            // Assert: Should be empty
            favoritesAfterRemoveResult!.Items.Should().BeEmpty();
            favoritesAfterRemoveResult.TotalCount.Should().Be(0);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ShouldFail()
        {
            // Arrange: Register first user
            var email = $"duplicate.{Guid.NewGuid():N}@example.com";
            var registerRequest = new
            {
                email,
                password = "TestPass123!",
                firstName = "First",
                lastName = "User"
            };

            var firstResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
            firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act: Try to register again with same email
            var duplicateRequest = new
            {
                email,
                password = "DifferentPass123!",
                firstName = "Second",
                lastName = "User"
            };

            var duplicateResponse = await _client.PostAsJsonAsync("/auth/register", duplicateRequest);

            // Assert: Should return 409 Conflict
            duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ShouldFail()
        {
            // Arrange
            var loginRequest = new
            {
                email = "nonexistent@example.com",
                password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

            // Assert: Should return 401 Unauthorized
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetProfile_WithoutAuth_ShouldFail()
        {
            // Act: Try to get profile without token
            var response = await _client.GetAsync("/auth/me");

            // Assert: Should return 401 Unauthorized
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AddFavorite_WithoutAuth_ShouldFail()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var request = new { productId };

            // Act: Try to add favorite without token
            var response = await _client.PostAsJsonAsync("/me/favorites", request);

            // Assert: Should return 401 Unauthorized
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AddFavorite_NonExistentProduct_ShouldFail()
        {
            // Arrange: Register and login
            var (token, _) = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", token);

            // Act: Try to add non-existent product
            var productId = Guid.NewGuid();
            var request = new { productId };
            var response = await _client.PostAsJsonAsync("/me/favorites", request);

            // Assert: Should return 404 Not Found
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

        // ==================== DTOs ====================

        private record AuthResponse(
            string Token,
            DateTime ExpiresAt,
            UserDto User
        );

        private record UserDto(
            Guid Id,
            string Email,
            string FirstName,
            string LastName,
            string? PhoneNumber,
            DateTime CreatedAt,
            bool IsActive
        );

        private record UserProfileDto(
            Guid Id,
            string Email,
            string FirstName,
            string LastName,
            string? PhoneNumber,
            string? DocumentType,
            string? DocumentNumber,
            DateTime? BirthDate,
            string? Address,
            string? City,
            string? Country,
            DateTime CreatedAt,
            bool IsActive
        );

        private record ProductListResponse(
            List<ProductDto> Items,
            int TotalCount,
            int Page,
            int PageSize,
            int TotalPages
        );

        private record ProductDto(
            Guid Id,
            string Name,
            string Description,
            decimal Price,
            int Stock,
            bool IsActive
        );

        private record AddFavoriteResponse(
            Guid FavoriteId,
            Guid ProductId,
            string Message
        );

        private record FavoriteListResponse(
            List<FavoriteProductDto> Items,
            int TotalCount
        );

        private record FavoriteProductDto(
            Guid ProductId,
            string ProductName,
            decimal Price,
            string? MainImageUrl,
            DateTime AddedAt,
            bool IsActive
        );

        private record CheckFavoriteResponse(
            bool IsFavorite
        );
    }
}
