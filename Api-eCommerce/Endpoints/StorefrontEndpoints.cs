using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Api_eCommerce.Endpoints
{
  /// <summary>
  /// Endpoints públicos del storefront (no requieren autenticación).
  /// Usados por la PWA/Web del cliente para mostrar el catálogo.
  /// Requieren X-Tenant-Slug para resolver el tenant.
  /// </summary>
  public static class StorefrontEndpoints
  {
    public static IEndpointRouteBuilder MapStorefrontEndpoints(this IEndpointRouteBuilder app)
    {
      var group = app.MapGroup("/api/store")
          .WithTags("Storefront (Public)")
          .AllowAnonymous();

      // ==================== BANNERS ====================
      group.MapGet("/banners", GetBanners)
          .WithName("GetStoreBanners")
          .WithSummary("Get active banners for the store")
          .WithDescription("Returns banners filtered by position, respecting date range scheduling")
          .Produces<List<StoreBannerDto>>(StatusCodes.Status200OK);

      // ==================== CATEGORIES ====================
      group.MapGet("/categories", GetCategories)
          .WithName("GetStoreCategories")
          .WithSummary("Get category tree")
          .WithDescription("Returns all active categories with hierarchy (parent/children)")
          .Produces<List<StoreCategoryDto>>(StatusCodes.Status200OK);

      group.MapGet("/categories/{slug}", GetCategoryBySlug)
          .WithName("GetCategoryBySlug")
          .WithSummary("Get category by slug")
          .Produces<StoreCategoryDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

      // ==================== PRODUCTS ====================
      group.MapGet("/products", GetProducts)
          .WithName("GetStoreProducts")
          .WithSummary("Get products with filters and pagination")
          .WithDescription("Filter by category, search, price range, featured, etc.")
          .Produces<StoreProductListResponse>(StatusCodes.Status200OK);

      group.MapGet("/products/featured", GetFeaturedProducts)
          .WithName("GetFeaturedProducts")
          .WithSummary("Get featured products for homepage")
          .Produces<List<StoreProductDto>>(StatusCodes.Status200OK);

      group.MapGet("/products/search", SearchProducts)
          .WithName("SearchStoreProducts")
          .WithSummary("Typeahead/autocomplete search")
          .WithDescription("Returns products matching the query (name, tags, sku)")
          .Produces<List<StoreProductSearchResult>>(StatusCodes.Status200OK);

      group.MapGet("/products/{slug}", GetProductBySlug)
          .WithName("GetProductBySlug")
          .WithSummary("Get product detail by slug")
          .Produces<StoreProductDetailDto>(StatusCodes.Status200OK)
          .Produces(StatusCodes.Status404NotFound);

      return app;
    }

    // ==================== BANNER HANDLERS ====================

    private static async Task<IResult> GetBanners(
        HttpContext context,
        TenantDbContextFactory dbFactory,
        ITenantResolver tenantResolver,
        [FromQuery] BannerPosition? position = null)
    {
      var tenant = await tenantResolver.ResolveAsync(context);
      if (tenant == null) return Results.Problem(statusCode: 400, detail: "Tenant not resolved");

      await using var db = dbFactory.Create();
      var now = DateTime.UtcNow;

      var query = db.Banners
          .AsNoTracking()
          .Where(b => b.IsActive)
          .Where(b => b.StartDate == null || b.StartDate <= now)
          .Where(b => b.EndDate == null || b.EndDate >= now);

      if (position.HasValue)
      {
        query = query.Where(b => b.Position == position.Value);
      }

      var banners = await query
          .OrderBy(b => b.DisplayOrder)
          .Select(b => new StoreBannerDto
          {
            Id = b.Id,
            Title = b.Title,
            Subtitle = b.Subtitle,
            ImageUrlDesktop = b.ImageUrlDesktop,
            ImageUrlMobile = b.ImageUrlMobile,
            TargetUrl = b.TargetUrl,
            ButtonText = b.ButtonText,
            Position = b.Position.ToString().ToLower()
          })
          .ToListAsync();

      return Results.Ok(banners);
    }

    // ==================== CATEGORY HANDLERS ====================

    private static async Task<IResult> GetCategories(
        HttpContext context,
        TenantDbContextFactory dbFactory,
        ITenantResolver tenantResolver,
        [FromQuery] bool includeInactive = false)
    {
      var tenant = await tenantResolver.ResolveAsync(context);
      if (tenant == null) return Results.Problem(statusCode: 400, detail: "Tenant not resolved");

      await using var db = dbFactory.Create();

      var query = db.Categories
          .AsNoTracking()
          .Include(c => c.Children)
          .Where(c => c.ParentId == null); // Solo categorías raíz

      if (!includeInactive)
      {
        query = query.Where(c => c.IsActive);
      }

      var categories = await query
          .OrderBy(c => c.DisplayOrder)
          .ThenBy(c => c.Name)
          .ToListAsync();

      var result = categories.Select(c => MapCategoryToDto(c, includeInactive)).ToList();
      return Results.Ok(result);
    }

    private static async Task<IResult> GetCategoryBySlug(
        string slug,
        HttpContext context,
        TenantDbContextFactory dbFactory,
        ITenantResolver tenantResolver)
    {
      var tenant = await tenantResolver.ResolveAsync(context);
      if (tenant == null) return Results.Problem(statusCode: 400, detail: "Tenant not resolved");

      await using var db = dbFactory.Create();

      var category = await db.Categories
          .AsNoTracking()
          .Include(c => c.Children)
          .Include(c => c.Parent)
          .FirstOrDefaultAsync(c => c.Slug == slug.ToLower() && c.IsActive);

      if (category == null)
        return Results.NotFound(new { error = "Category not found", slug });

      // Obtener productos de la categoría
      var productCount = await db.ProductCategories
          .CountAsync(pc => pc.CategoryId == category.Id);

      return Results.Ok(new StoreCategoryDetailDto
      {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        Description = category.Description,
        ImageUrl = category.ImageUrl,
        ParentSlug = category.Parent?.Slug,
        ParentName = category.Parent?.Name,
        Children = category.Children?
              .Where(c => c.IsActive)
              .OrderBy(c => c.DisplayOrder)
              .Select(c => new StoreCategoryDto
              {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ImageUrl = c.ImageUrl,
                ProductCount = 0 // Se podría calcular si es necesario
              }).ToList() ?? new(),
        ProductCount = productCount,
        MetaTitle = category.MetaTitle ?? category.Name,
        MetaDescription = category.MetaDescription ?? category.Description
      });
    }

    // ==================== PRODUCT HANDLERS ====================

    private static async Task<IResult> GetProducts(
        HttpContext context,
        TenantDbContextFactory dbFactory,
        ITenantResolver tenantResolver,
        [FromQuery] string? category = null,      // slug de categoría
        [FromQuery] string? search = null,        // búsqueda general
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? brand = null,
        [FromQuery] bool? featured = null,
        [FromQuery] bool? inStock = null,
        [FromQuery] string? sortBy = null,        // price, name, newest
        [FromQuery] string? sortOrder = null,     // asc, desc
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
      var tenant = await tenantResolver.ResolveAsync(context);
      if (tenant == null) return Results.Problem(statusCode: 400, detail: "Tenant not resolved");

      await using var db = dbFactory.Create();

      // Query base - solo productos activos
      var query = db.Products
          .AsNoTracking()
          .Where(p => p.IsActive);

      // Filtro por categoría
      if (!string.IsNullOrWhiteSpace(category))
      {
        var categoryEntity = await db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == category.ToLower());

        if (categoryEntity != null)
        {
          var productIds = db.ProductCategories
              .Where(pc => pc.CategoryId == categoryEntity.Id)
              .Select(pc => pc.ProductId);

          query = query.Where(p => productIds.Contains(p.Id));
        }
      }

      // Búsqueda por texto
      if (!string.IsNullOrWhiteSpace(search))
      {
        var searchLower = search.ToLower();
        query = query.Where(p =>
            p.Name.ToLower().Contains(searchLower) ||
            (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
            (p.Tags != null && p.Tags.ToLower().Contains(searchLower)) ||
            (p.Sku != null && p.Sku.ToLower().Contains(searchLower)) ||
            (p.Brand != null && p.Brand.ToLower().Contains(searchLower)));
      }

      // Filtro por precio
      if (minPrice.HasValue)
        query = query.Where(p => p.Price >= minPrice.Value);

      if (maxPrice.HasValue)
        query = query.Where(p => p.Price <= maxPrice.Value);

      // Filtro por marca
      if (!string.IsNullOrWhiteSpace(brand))
        query = query.Where(p => p.Brand != null && p.Brand.ToLower() == brand.ToLower());

      // Filtro por destacados
      if (featured.HasValue)
        query = query.Where(p => p.IsFeatured == featured.Value);

      // Filtro por stock
      if (inStock == true)
        query = query.Where(p => !p.TrackInventory || p.Stock > 0);

      // Contar total antes de paginar
      var totalItems = await query.CountAsync();

      // Ordenamiento
      query = (sortBy?.ToLower(), sortOrder?.ToLower()) switch
      {
        ("price", "desc") => query.OrderByDescending(p => p.Price),
        ("price", _) => query.OrderBy(p => p.Price),
        ("name", "desc") => query.OrderByDescending(p => p.Name),
        ("name", _) => query.OrderBy(p => p.Name),
        ("newest", _) => query.OrderByDescending(p => p.CreatedAt),
        _ => query.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt)
      };

      // Paginación
      pageSize = Math.Min(pageSize, 50); // Máximo 50 por página
      var skip = (page - 1) * pageSize;

      var products = await query
          .Skip(skip)
          .Take(pageSize)
          .Select(p => new StoreProductDto
          {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            ShortDescription = p.ShortDescription,
            Price = p.Price,
            CompareAtPrice = p.CompareAtPrice,
            MainImageUrl = p.MainImageUrl,
            Brand = p.Brand,
            InStock = !p.TrackInventory || p.Stock > 0,
            IsFeatured = p.IsFeatured
          })
          .ToListAsync();

      return Results.Ok(new StoreProductListResponse
      {
        Items = products,
        Page = page,
        PageSize = pageSize,
        TotalItems = totalItems,
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
      });
    }

    private static async Task<IResult> GetFeaturedProducts(
        HttpContext context,
        TenantDbContextFactory dbFactory,
        ITenantResolver tenantResolver,
        [FromQuery] int limit = 8)
    {
      var tenant = await tenantResolver.ResolveAsync(context);
      if (tenant == null) return Results.Problem(statusCode: 400, detail: "Tenant not resolved");

      await using var db = dbFactory.Create();

      limit = Math.Min(limit, 20); // Máximo 20

      var products = await db.Products
          .AsNoTracking()
          .Where(p => p.IsActive && p.IsFeatured)
          .OrderByDescending(p => p.CreatedAt)
          .Take(limit)
          .Select(p => new StoreProductDto
          {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            ShortDescription = p.ShortDescription,
            Price = p.Price,
            CompareAtPrice = p.CompareAtPrice,
            MainImageUrl = p.MainImageUrl,
            Brand = p.Brand,
            InStock = !p.TrackInventory || p.Stock > 0,
            IsFeatured = true
          })
          .ToListAsync();

      return Results.Ok(products);
    }

    /// <summary>
    /// Typeahead/Autocomplete search - optimizado para respuesta rápida
    /// </summary>
    private static async Task<IResult> SearchProducts(
        HttpContext context,
        TenantDbContextFactory dbFactory,
        ITenantResolver tenantResolver,
        [FromQuery] string q,
        [FromQuery] int limit = 10)
    {
      if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
      {
        return Results.Ok(new List<StoreProductSearchResult>());
      }

      var tenant = await tenantResolver.ResolveAsync(context);
      if (tenant == null) return Results.Problem(statusCode: 400, detail: "Tenant not resolved");

      await using var db = dbFactory.Create();

      limit = Math.Min(limit, 20);
      var searchLower = q.ToLower().Trim();

      // Búsqueda rápida - prioriza coincidencias al inicio del nombre
      var products = await db.Products
          .AsNoTracking()
          .Where(p => p.IsActive)
          .Where(p =>
              p.Name.ToLower().Contains(searchLower) ||
              (p.Sku != null && p.Sku.ToLower().Contains(searchLower)) ||
              (p.Tags != null && p.Tags.ToLower().Contains(searchLower)))
          .OrderBy(p => p.Name.ToLower().StartsWith(searchLower) ? 0 : 1) // Priorizar inicio
          .ThenBy(p => p.Name)
          .Take(limit)
          .Select(p => new StoreProductSearchResult
          {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Price = p.Price,
            ImageUrl = p.MainImageUrl,
            Sku = p.Sku
          })
          .ToListAsync();

      return Results.Ok(products);
    }

    private static async Task<IResult> GetProductBySlug(
        string slug,
        HttpContext context,
        TenantDbContextFactory dbFactory,
        ITenantResolver tenantResolver)
    {
      var tenant = await tenantResolver.ResolveAsync(context);
      if (tenant == null) return Results.Problem(statusCode: 400, detail: "Tenant not resolved");

      await using var db = dbFactory.Create();

      var product = await db.Products
          .AsNoTracking()
          .Include(p => p.Images)
          .Include(p => p.Categories)
          .FirstOrDefaultAsync(p => p.Slug == slug.ToLower() && p.IsActive);

      if (product == null)
        return Results.NotFound(new { error = "Product not found", slug });

      // Obtener categorías
      var categoryIds = product.Categories?.Select(pc => pc.CategoryId).ToList() ?? new();
      var categories = await db.Categories
          .AsNoTracking()
          .Where(c => categoryIds.Contains(c.Id))
          .Select(c => new { c.Name, c.Slug })
          .ToListAsync();

      return Results.Ok(new StoreProductDetailDto
      {
        Id = product.Id,
        Name = product.Name,
        Slug = product.Slug,
        Sku = product.Sku,
        Description = product.Description,
        ShortDescription = product.ShortDescription,
        Price = product.Price,
        CompareAtPrice = product.CompareAtPrice,
        Brand = product.Brand,
        Stock = product.TrackInventory ? product.Stock : null,
        InStock = !product.TrackInventory || product.Stock > 0,
        IsFeatured = product.IsFeatured,
        Images = product.Images?
              .OrderBy(i => i.Order)
              .Select(i => new StoreProductImageDto
              {
                Id = i.Id,
                Url = i.ImageUrl,
                IsPrimary = i.IsPrimary
              }).ToList() ?? new(),
        Categories = categories.Select(c => new StoreCategoryRefDto
        {
          Name = c.Name,
          Slug = c.Slug
        }).ToList(),
        Tags = product.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new(),
        MetaTitle = product.MetaTitle ?? product.Name,
        MetaDescription = product.MetaDescription ?? product.ShortDescription
      });
    }

    // ==================== HELPERS ====================

    private static StoreCategoryDto MapCategoryToDto(Category category, bool includeInactive)
    {
      return new StoreCategoryDto
      {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        ImageUrl = category.ImageUrl,
        Children = category.Children?
              .Where(c => includeInactive || c.IsActive)
              .OrderBy(c => c.DisplayOrder)
              .ThenBy(c => c.Name)
              .Select(c => MapCategoryToDto(c, includeInactive))
              .ToList() ?? new()
      };
    }
  }

  #region DTOs

  // ==================== BANNER DTOs ====================
  public record StoreBannerDto
  {
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public string ImageUrlDesktop { get; init; } = string.Empty;
    public string? ImageUrlMobile { get; init; }
    public string? TargetUrl { get; init; }
    public string? ButtonText { get; init; }
    public string Position { get; init; } = "hero";
  }

  // ==================== CATEGORY DTOs ====================
  public record StoreCategoryDto
  {
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int ProductCount { get; init; }
    public List<StoreCategoryDto> Children { get; init; } = new();
  }

  public record StoreCategoryDetailDto
  {
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public string? ParentSlug { get; init; }
    public string? ParentName { get; init; }
    public List<StoreCategoryDto> Children { get; init; } = new();
    public int ProductCount { get; init; }
    public string MetaTitle { get; init; } = string.Empty;
    public string? MetaDescription { get; init; }
  }

  public record StoreCategoryRefDto
  {
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
  }

  // ==================== PRODUCT DTOs ====================
  public record StoreProductDto
  {
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
    public decimal Price { get; init; }
    public decimal? CompareAtPrice { get; init; } // Precio anterior tachado
    public string? MainImageUrl { get; init; }
    public string? Brand { get; init; }
    public bool InStock { get; init; }
    public bool IsFeatured { get; init; }
  }

  public record StoreProductDetailDto
  {
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public string? Description { get; init; }
    public string? ShortDescription { get; init; }
    public decimal Price { get; init; }
    public decimal? CompareAtPrice { get; init; }
    public string? Brand { get; init; }
    public int? Stock { get; init; } // null si no se trackea inventario
    public bool InStock { get; init; }
    public bool IsFeatured { get; init; }
    public List<StoreProductImageDto> Images { get; init; } = new();
    public List<StoreCategoryRefDto> Categories { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public string MetaTitle { get; init; } = string.Empty;
    public string? MetaDescription { get; init; }
  }

  public record StoreProductImageDto
  {
    public Guid Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
  }

  public record StoreProductSearchResult
  {
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string? ImageUrl { get; init; }
    public string? Sku { get; init; }
  }

  public record StoreProductListResponse
  {
    public List<StoreProductDto> Items { get; init; } = new();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
  }

  #endregion
}
