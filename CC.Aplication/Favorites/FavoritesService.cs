using CC.Domain.Favorites;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Favorites
{
    public interface IFavoritesService
    {
        Task<FavoriteListResponse> GetUserFavoritesAsync(Guid userId, CancellationToken ct = default);
        Task<AddFavoriteResponse> AddFavoriteAsync(Guid userId, Guid productId, CancellationToken ct = default);
        Task<bool> RemoveFavoriteAsync(Guid userId, Guid productId, CancellationToken ct = default);
        Task<bool> IsFavoriteAsync(Guid userId, Guid productId, CancellationToken ct = default);
    }

    public class FavoritesService : IFavoritesService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ITenantAccessor _tenantAccessor;
        private readonly ILogger<FavoritesService> _logger;

        public FavoritesService(
            TenantDbContextFactory dbFactory,
            ITenantAccessor tenantAccessor,
            ILogger<FavoritesService> logger)
        {
            _dbFactory = dbFactory;
            _tenantAccessor = tenantAccessor;
            _logger = logger;
        }

        public async Task<FavoriteListResponse> GetUserFavoritesAsync(Guid userId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Obtener favoritos del usuario con información del producto
            var favorites = await db.FavoriteProducts
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    f.ProductId,
                    f.CreatedAt,
                    Product = db.Products
                        .Where(p => p.Id == f.ProductId)
                        .Select(p => new
                        {
                            p.Name,
                            p.Price,
                            p.IsActive,
                            MainImage = db.ProductImages
                                .Where(i => i.ProductId == p.Id && i.IsPrimary)
                                .Select(i => i.ImageUrl)
                                .FirstOrDefault()
                        })
                        .FirstOrDefault()
                })
                .AsNoTracking()
                .ToListAsync(ct);

            var favoriteDtos = favorites
                .Where(f => f.Product != null) // Filtrar productos que ya no existen
                .Select(f => new FavoriteProductDto(
                    f.ProductId,
                    f.Product!.Name,
                    f.Product.Price,
                    f.Product.MainImage,
                    f.CreatedAt,
                    f.Product.IsActive
                ))
                .ToList();

            return new FavoriteListResponse(
                favoriteDtos,
                favoriteDtos.Count
            );
        }

        public async Task<AddFavoriteResponse> AddFavoriteAsync(Guid userId, Guid productId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Verificar que el producto existe y está activo
            var product = await db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId, ct);

            if (product == null)
            {
                throw new InvalidOperationException($"Product {productId} not found");
            }

            if (!product.IsActive)
            {
                throw new InvalidOperationException("Cannot add inactive product to favorites");
            }

            // Verificar si ya existe (idempotente)
            var existingFavorite = await db.FavoriteProducts
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, ct);

            if (existingFavorite != null)
            {
                _logger.LogInformation(
                    "Product {ProductId} already in favorites for user {UserId}",
                    productId, userId);

                return new AddFavoriteResponse(
                    existingFavorite.Id,
                    productId,
                    "Product already in favorites"
                );
            }

            // Crear nuevo favorito
            var favorite = new FavoriteProduct
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            };

            db.FavoriteProducts.Add(favorite);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Product {ProductId} added to favorites for user {UserId}",
                productId, userId);

            return new AddFavoriteResponse(
                favorite.Id,
                productId,
                "Product added to favorites"
            );
        }

        public async Task<bool> RemoveFavoriteAsync(Guid userId, Guid productId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            // Buscar favorito
            var favorite = await db.FavoriteProducts
                .FirstOrDefaultAsync(f => f.UserId == userId && f.ProductId == productId, ct);

            if (favorite == null)
            {
                _logger.LogWarning(
                    "Favorite not found for user {UserId} and product {ProductId}",
                    userId, productId);
                return false;
            }

            // Eliminar
            db.FavoriteProducts.Remove(favorite);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Product {ProductId} removed from favorites for user {UserId}",
                productId, userId);

            return true;
        }

        public async Task<bool> IsFavoriteAsync(Guid userId, Guid productId, CancellationToken ct = default)
        {
            if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            await using var db = _dbFactory.Create();

            return await db.FavoriteProducts
                .AnyAsync(f => f.UserId == userId && f.ProductId == productId, ct);
        }
    }
}
