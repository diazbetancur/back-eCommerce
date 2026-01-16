using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CC.Aplication.Catalog
{
  /// <summary>
  /// Servicio de gestión de productos del catálogo
  /// </summary>
  public interface IProductService
  {
    Task<ProductResponseDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default);
    Task<ProductResponseDto> UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken ct = default);
    Task<ProductResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductResponseDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<PagedResult<ProductResponseDto>> GetPagedAsync(ProductFilterDto filter, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> UpdateStockAsync(Guid id, int quantity, CancellationToken ct = default);
    Task<bool> ToggleFeaturedAsync(Guid id, CancellationToken ct = default);
  }

  public class ProductService : IProductService
  {
    private readonly TenantDbContextFactory _dbFactory;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        TenantDbContextFactory dbFactory,
        ITenantAccessor tenantAccessor,
        ILogger<ProductService> logger)
    {
      _dbFactory = dbFactory;
      _tenantAccessor = tenantAccessor;
      _logger = logger;
    }

    /// <summary>
    /// Crear nuevo producto
    /// </summary>
    public async Task<ProductResponseDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      // Generar slug único
      var slug = await GenerateUniqueSlugAsync(db, dto.Name, ct);

      var product = new Product
      {
        Id = Guid.NewGuid(),
        Name = dto.Name.Trim(),
        Slug = slug,
        Sku = dto.Sku?.Trim(),
        Description = dto.Description?.Trim(),
        ShortDescription = dto.ShortDescription?.Trim(),
        Price = dto.Price,
        CompareAtPrice = dto.CompareAtPrice,
        Stock = dto.Stock,
        TrackInventory = dto.TrackInventory,
        IsActive = dto.IsActive,
        IsFeatured = dto.IsFeatured,
        Tags = dto.Tags?.Trim(),
        Brand = dto.Brand?.Trim(),
        MetaTitle = dto.MetaTitle?.Trim(),
        MetaDescription = dto.MetaDescription?.Trim(),
        MainImageUrl = dto.MainImageUrl?.Trim(),
        CreatedAt = DateTime.UtcNow
      };

      db.Products.Add(product);
      await db.SaveChangesAsync(ct);

      // Asociar con categorías si se proporcionaron
      if (dto.CategoryIds != null && dto.CategoryIds.Any())
      {
        foreach (var categoryId in dto.CategoryIds.Distinct())
        {
          // Verificar que la categoría existe
          var categoryExists = await db.Categories.AnyAsync(c => c.Id == categoryId, ct);
          if (categoryExists)
          {
            db.ProductCategories.Add(new ProductCategory
            {
              ProductId = product.Id,
              CategoryId = categoryId
            });
          }
        }
        await db.SaveChangesAsync(ct);
      }

      _logger.LogInformation("✅ Product created: {ProductId} - {Name}", product.Id, product.Name);

      return MapToDto(product);
    }

    /// <summary>
    /// Actualizar producto existente
    /// </summary>
    public async Task<ProductResponseDto> UpdateAsync(Guid id, UpdateProductDto dto, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      var product = await db.Products.FindAsync(new object[] { id }, ct);

      if (product == null)
      {
        throw new InvalidOperationException($"Product {id} not found");
      }

      // Actualizar slug si cambió el nombre
      if (!string.IsNullOrEmpty(dto.Name) && dto.Name != product.Name)
      {
        product.Slug = await GenerateUniqueSlugAsync(db, dto.Name, ct, product.Id);
      }

      // Actualizar campos
      product.Name = dto.Name?.Trim() ?? product.Name;
      product.Sku = dto.Sku?.Trim() ?? product.Sku;
      product.Description = dto.Description?.Trim() ?? product.Description;
      product.ShortDescription = dto.ShortDescription?.Trim() ?? product.ShortDescription;
      product.Price = dto.Price ?? product.Price;
      product.CompareAtPrice = dto.CompareAtPrice ?? product.CompareAtPrice;
      product.Stock = dto.Stock ?? product.Stock;
      product.TrackInventory = dto.TrackInventory ?? product.TrackInventory;
      product.IsActive = dto.IsActive ?? product.IsActive;
      product.IsFeatured = dto.IsFeatured ?? product.IsFeatured;
      product.Tags = dto.Tags?.Trim() ?? product.Tags;
      product.Brand = dto.Brand?.Trim() ?? product.Brand;
      product.MetaTitle = dto.MetaTitle?.Trim() ?? product.MetaTitle;
      product.MetaDescription = dto.MetaDescription?.Trim() ?? product.MetaDescription;
      product.MainImageUrl = dto.MainImageUrl?.Trim() ?? product.MainImageUrl;
      product.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);

      // Actualizar categorías si se proporcionaron
      if (dto.CategoryIds != null)
      {
        // Remover asociaciones existentes
        var existingCategories = await db.ProductCategories
            .Where(pc => pc.ProductId == id)
            .ToListAsync(ct);
        db.ProductCategories.RemoveRange(existingCategories);

        // Agregar nuevas asociaciones
        foreach (var categoryId in dto.CategoryIds.Distinct())
        {
          // Verificar que la categoría existe
          var categoryExists = await db.Categories.AnyAsync(c => c.Id == categoryId, ct);
          if (categoryExists)
          {
            db.ProductCategories.Add(new ProductCategory
            {
              ProductId = id,
              CategoryId = categoryId
            });
          }
        }
        await db.SaveChangesAsync(ct);
      }

      _logger.LogInformation("✅ Product updated: {ProductId} - {Name}", product.Id, product.Name);

      return MapToDto(product);
    }

    /// <summary>
    /// Obtener producto por ID
    /// </summary>
    public async Task<ProductResponseDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      var product = await db.Products
          .AsNoTracking()
          .Include(p => p.Images)
          .FirstOrDefaultAsync(p => p.Id == id, ct);

      if (product == null)
        return null;

      return await MapToDtoWithCategoriesAsync(db, product, ct);
    }

    /// <summary>
    /// Obtener producto por slug (SEO-friendly URL)
    /// </summary>
    public async Task<ProductResponseDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      var product = await db.Products
          .AsNoTracking()
          .Include(p => p.Images)
          .FirstOrDefaultAsync(p => p.Slug == slug.ToLower(), ct);

      if (product == null)
        return null;

      return await MapToDtoWithCategoriesAsync(db, product, ct);
    }

    /// <summary>
    /// Obtener productos con paginación y filtros
    /// </summary>
    public async Task<PagedResult<ProductResponseDto>> GetPagedAsync(ProductFilterDto filter, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      var query = db.Products.AsNoTracking();

      // Filtros
      if (!string.IsNullOrWhiteSpace(filter.Search))
      {
        var search = filter.Search.ToLower();
        query = query.Where(p =>
            p.Name.ToLower().Contains(search) ||
            (p.Description != null && p.Description.ToLower().Contains(search)) ||
            (p.Sku != null && p.Sku.ToLower().Contains(search)));
      }

      if (filter.CategoryId.HasValue)
      {
        query = query.Where(p => p.Categories!.Any(c => c.CategoryId == filter.CategoryId.Value));
      }

      if (filter.IsActive.HasValue)
      {
        query = query.Where(p => p.IsActive == filter.IsActive.Value);
      }

      if (filter.IsFeatured.HasValue)
      {
        query = query.Where(p => p.IsFeatured == filter.IsFeatured.Value);
      }

      if (filter.MinPrice.HasValue)
      {
        query = query.Where(p => p.Price >= filter.MinPrice.Value);
      }

      if (filter.MaxPrice.HasValue)
      {
        query = query.Where(p => p.Price <= filter.MaxPrice.Value);
      }

      if (filter.InStock.HasValue && filter.InStock.Value)
      {
        query = query.Where(p => p.Stock > 0);
      }

      if (!string.IsNullOrWhiteSpace(filter.Brand))
      {
        query = query.Where(p => p.Brand != null && p.Brand.ToLower() == filter.Brand.ToLower());
      }

      // Total antes de paginar
      var totalCount = await query.CountAsync(ct);

      // Ordenamiento
      query = filter.SortBy?.ToLower() switch
      {
        "name" => filter.SortDesc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
        "price" => filter.SortDesc ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
        "stock" => filter.SortDesc ? query.OrderByDescending(p => p.Stock) : query.OrderBy(p => p.Stock),
        "created" => filter.SortDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
        _ => query.OrderByDescending(p => p.CreatedAt) // Default
      };

      // Paginación
      var products = await query
          .Skip((filter.Page - 1) * filter.PageSize)
          .Take(filter.PageSize)
          .Include(p => p.Images)
          .ToListAsync(ct);

      var items = products.Select(MapToDto).ToList();

      return new PagedResult<ProductResponseDto>
      {
        Items = items,
        TotalCount = totalCount,
        Page = filter.Page,
        PageSize = filter.PageSize,
        TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
      };
    }

    /// <summary>
    /// Eliminar producto (soft delete)
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      var product = await db.Products.FindAsync(new object[] { id }, ct);

      if (product == null)
      {
        return false;
      }

      // Soft delete: marcar como inactivo
      product.IsActive = false;
      product.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);

      _logger.LogInformation("✅ Product soft-deleted: {ProductId} - {Name}", product.Id, product.Name);

      return true;
    }

    /// <summary>
    /// Actualizar stock del producto
    /// </summary>
    public async Task<bool> UpdateStockAsync(Guid id, int quantity, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      var product = await db.Products.FindAsync(new object[] { id }, ct);

      if (product == null)
      {
        return false;
      }

      product.Stock = quantity;
      product.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);

      _logger.LogInformation("✅ Product stock updated: {ProductId} - Stock: {Stock}", product.Id, product.Stock);

      return true;
    }

    /// <summary>
    /// Alternar estado de producto destacado
    /// </summary>
    public async Task<bool> ToggleFeaturedAsync(Guid id, CancellationToken ct = default)
    {
      ValidateTenantContext();

      await using var db = _dbFactory.Create();

      var product = await db.Products.FindAsync(new object[] { id }, ct);

      if (product == null)
      {
        return false;
      }

      product.IsFeatured = !product.IsFeatured;
      product.UpdatedAt = DateTime.UtcNow;

      await db.SaveChangesAsync(ct);

      _logger.LogInformation("✅ Product featured toggled: {ProductId} - Featured: {IsFeatured}",
          product.Id, product.IsFeatured);

      return true;
    }

    // ==================== PRIVATE HELPERS ====================

    private void ValidateTenantContext()
    {
      if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
      {
        throw new InvalidOperationException("No tenant context available");
      }
    }

    /// <summary>
    /// Generar slug único a partir del nombre del producto
    /// </summary>
    private async Task<string> GenerateUniqueSlugAsync(
        TenantDbContext db,
        string name,
        CancellationToken ct,
        Guid? excludeId = null)
    {
      var baseSlug = GenerateSlug(name);
      var slug = baseSlug;
      var counter = 1;

      while (true)
      {
        var query = db.Products.Where(p => p.Slug == slug);

        if (excludeId.HasValue)
        {
          query = query.Where(p => p.Id != excludeId.Value);
        }

        var exists = await query.AnyAsync(ct);

        if (!exists)
        {
          break;
        }

        slug = $"{baseSlug}-{counter}";
        counter++;
      }

      return slug;
    }

    /// <summary>
    /// Convertir texto a slug (URL-friendly)
    /// Ejemplo: "Camisa Azul Premium" -> "camisa-azul-premium"
    /// </summary>
    private static string GenerateSlug(string text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return string.Empty;

      // Convertir a lowercase
      var slug = text.ToLowerInvariant();

      // Reemplazar caracteres especiales
      slug = slug.Replace("á", "a").Replace("é", "e").Replace("í", "i")
                 .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");

      // Remover caracteres no alfanuméricos (excepto espacios y guiones)
      slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");

      // Reemplazar espacios múltiples por uno solo
      slug = Regex.Replace(slug, @"\s+", " ").Trim();

      // Reemplazar espacios por guiones
      slug = slug.Replace(" ", "-");

      // Remover guiones múltiples
      slug = Regex.Replace(slug, @"-+", "-");

      return slug;
    }

    private static ProductResponseDto MapToDto(Product product)
    {
      return new ProductResponseDto
      {
        Id = product.Id,
        Name = product.Name,
        Slug = product.Slug,
        Sku = product.Sku,
        Description = product.Description,
        ShortDescription = product.ShortDescription,
        Price = product.Price,
        CompareAtPrice = product.CompareAtPrice,
        Stock = product.Stock,
        TrackInventory = product.TrackInventory,
        IsActive = product.IsActive,
        IsFeatured = product.IsFeatured,
        Tags = product.Tags,
        Brand = product.Brand,
        MetaTitle = product.MetaTitle,
        MetaDescription = product.MetaDescription,
        MainImageUrl = product.MainImageUrl,
        CreatedAt = product.CreatedAt,
        UpdatedAt = product.UpdatedAt,
        Categories = new List<CategorySummaryDto>(),
        Images = product.Images?.Select(img => new ProductImageDto
        {
          Id = img.Id,
          ImageUrl = img.ImageUrl,
          Order = img.Order,
          IsPrimary = img.IsPrimary
        }).ToList() ?? new List<ProductImageDto>()
      };
    }

    private static async Task<ProductResponseDto> MapToDtoWithCategoriesAsync(
        TenantDbContext db,
        Product product,
        CancellationToken ct)
    {
      var dto = MapToDto(product);

      // Cargar categorías asociadas
      var categories = await db.ProductCategories
          .Where(pc => pc.ProductId == product.Id)
          .Join(db.Categories,
              pc => pc.CategoryId,
              c => c.Id,
              (pc, c) => new CategorySummaryDto
              {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug
              })
          .ToListAsync(ct);

      dto.Categories = categories;
      return dto;
    }
  }

  // ==================== DTOs ====================

  public record CreateProductDto
  {
    public required string Name { get; init; }
    public string? Sku { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public decimal Price { get; init; }
    public decimal? CompareAtPrice { get; init; }
    public int Stock { get; init; } = 0;
    public bool TrackInventory { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public bool IsFeatured { get; init; } = false;
    public string? Tags { get; init; }
    public string? Brand { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? MainImageUrl { get; init; }
    public List<Guid>? CategoryIds { get; init; } // IDs de categorías a asociar
  }

  public record UpdateProductDto
  {
    public string? Name { get; init; }
    public string? Sku { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public decimal? Price { get; init; }
    public decimal? CompareAtPrice { get; init; }
    public int? Stock { get; init; }
    public bool? TrackInventory { get; init; }
    public bool? IsActive { get; init; }
    public bool? IsFeatured { get; init; }
    public string? Tags { get; init; }
    public string? Brand { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? MainImageUrl { get; init; }
    public List<Guid>? CategoryIds { get; init; } // IDs de categorías a asociar (null = no modificar)
  }

  public class ProductResponseDto
  {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int Stock { get; set; }
    public bool TrackInventory { get; set; }
    public bool IsActive { get; set; }
    public bool IsFeatured { get; set; }
    public string? Tags { get; set; }
    public string? Brand { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MainImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<CategorySummaryDto> Categories { get; set; } = new();
    public List<ProductImageDto> Images { get; set; } = new();
  }

  public class CategorySummaryDto
  {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
  }

  public class ProductFilterDto
  {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsFeatured { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
    public string? Brand { get; set; }
    public string? SortBy { get; set; } = "created"; // name, price, stock, created
    public bool SortDesc { get; set; } = true;
  }

  public class PagedResult<T>
  {
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
  }
}
