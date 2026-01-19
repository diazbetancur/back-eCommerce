namespace CC.Domain.Dto
{
  /// <summary>
  /// DTOs para la tienda pública (storefront)
  /// </summary>

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
    public decimal? CompareAtPrice { get; init; }
    public string? MainImageUrl { get; init; }
    public string? Brand { get; init; }
    public bool InStock { get; init; }
    public bool IsFeatured { get; init; }
    public List<StoreCategoryRefDto> Categories { get; init; } = new();
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
    public int? Stock { get; init; }
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
}
