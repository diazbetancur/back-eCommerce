using CC.Aplication.Catalog;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CC.Aplication.Services
{
    public interface ICatalogService
    {
        Task<List<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken ct = default);
        Task<List<ProductDto>> SearchProductsAsync(string query, CancellationToken ct = default);
        Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default);
    }

    public class CatalogService : ICatalogService
    {
        private readonly TenantDbContextFactory _dbFactory;
        private readonly ILogger<CatalogService> _logger;

        public CatalogService(TenantDbContextFactory dbFactory, ILogger<CatalogService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<List<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var products = await db.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    IsActive = p.IsActive
                })
                .ToListAsync(ct);

            return products;
        }

        public async Task<ProductDto?> GetProductByIdAsync(Guid productId, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var product = await db.Products
                .Where(p => p.Id == productId && p.IsActive)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    IsActive = p.IsActive
                })
                .FirstOrDefaultAsync(ct);

            return product;
        }

        public async Task<List<ProductDto>> SearchProductsAsync(string query, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var normalizedQuery = query.ToLower();
            var products = await db.Products
                .Where(p => p.IsActive && 
                    (p.Name.ToLower().Contains(normalizedQuery) || 
                     (p.Description != null && p.Description.ToLower().Contains(normalizedQuery))))
                .OrderBy(p => p.Name)
                .Take(50)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    Stock = p.Stock,
                    IsActive = p.IsActive
                })
                .ToListAsync(ct);

            return products;
        }

        public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
        {
            await using var db = _dbFactory.Create();

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                Stock = request.Stock,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Product created: {ProductId} - {ProductName}", product.Id, product.Name);

            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                IsActive = product.IsActive
            };
        }
    }
}
