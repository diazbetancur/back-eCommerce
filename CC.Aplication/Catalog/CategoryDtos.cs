using System.ComponentModel.DataAnnotations;

namespace CC.Aplication.Catalog
{
  /// <summary>
  /// Request para crear una categoría
  /// </summary>
  public record CreateCategoryRequest
  {
    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
    public string Name { get; init; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Description { get; init; }

    [Url(ErrorMessage = "La URL de la imagen no es válida")]
    public string? ImageUrl { get; init; }

    public bool IsActive { get; init; } = true;

    // Preparado para futuro: jerarquía
    public Guid? ParentId { get; init; }
  }

  /// <summary>
  /// Request para actualizar una categoría
  /// </summary>
  public record UpdateCategoryRequest
  {
    [Required]
    public Guid Id { get; init; }

    [Required(ErrorMessage = "El nombre es requerido")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
    public string Name { get; init; } = string.Empty;

    [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Description { get; init; }

    [Url(ErrorMessage = "La URL de la imagen no es válida")]
    public string? ImageUrl { get; init; }

    public bool IsActive { get; init; }

    // Preparado para futuro: jerarquía
    public Guid? ParentId { get; init; }
  }

  /// <summary>
  /// Response de categoría con información completa
  /// </summary>
  public record CategoryResponse
  {
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
    public int ProductCount { get; init; } // Cantidad de productos asociados
    public Guid? ParentId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
  }

  /// <summary>
  /// Response simplificada para listados
  /// </summary>
  public record CategoryListItem
  {
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
    public int ProductCount { get; init; }
  }

  /// <summary>
  /// Response paginada
  /// </summary>
  public record CategoryListResponse
  {
    public List<CategoryListItem> Items { get; init; } = new();
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
  }
}
