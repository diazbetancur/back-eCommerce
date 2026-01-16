using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CC.Aplication.Catalog
{
  public interface ICategoryManagementService
  {
    Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CategoryResponse?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<CategoryListResponse> GetAllAsync(int page, int pageSize, string? search, bool? isActive, CancellationToken ct = default);
  }

  public class CategoryManagementService : ICategoryManagementService
  {
    private readonly TenantDbContextFactory _dbFactory;
    private readonly ITenantAccessor _tenantAccessor;

    public CategoryManagementService(
        TenantDbContextFactory dbFactory,
        ITenantAccessor tenantAccessor)
    {
      _dbFactory = dbFactory;
      _tenantAccessor = tenantAccessor;
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("Tenant context not available");

      await using var db = _dbFactory.Create();

      // Generar slug único
      var slug = GenerateSlug(request.Name);
      var originalSlug = slug;
      var counter = 1;

      while (await db.Categories.AnyAsync(c => c.Slug == slug, ct))
      {
        slug = $"{originalSlug}-{counter++}";
      }

      // Validar nombre único
      if (await db.Categories.AnyAsync(c => c.Name.ToLower() == request.Name.ToLower(), ct))
      {
        throw new InvalidOperationException($"Ya existe una categoría con el nombre '{request.Name}'");
      }

      // Validar parent si se especifica (preparado para futuro)
      if (request.ParentId.HasValue)
      {
        var parentExists = await db.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct);
        if (!parentExists)
          throw new InvalidOperationException("La categoría padre no existe");
      }

      var category = new CC.Infraestructure.Tenant.Entities.Category
      {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Slug = slug,
        Description = request.Description,
        ImageUrl = request.ImageUrl,
        IsActive = request.IsActive,
        ParentId = request.ParentId
      };

      db.Categories.Add(category);
      await db.SaveChangesAsync(ct);

      return await GetByIdAsync(category.Id, ct)
          ?? throw new InvalidOperationException("Error al recuperar la categoría creada");
    }

    public async Task<CategoryResponse> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("Tenant context not available");

      await using var db = _dbFactory.Create();

      var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
      if (category == null)
        throw new InvalidOperationException("Categoría no encontrada");

      // Validar nombre único (excepto la misma categoría)
      if (await db.Categories.AnyAsync(c => c.Name.ToLower() == request.Name.ToLower() && c.Id != id, ct))
      {
        throw new InvalidOperationException($"Ya existe otra categoría con el nombre '{request.Name}'");
      }

      // Actualizar slug solo si cambió el nombre
      if (category.Name != request.Name)
      {
        var slug = GenerateSlug(request.Name);
        var originalSlug = slug;
        var counter = 1;

        while (await db.Categories.AnyAsync(c => c.Slug == slug && c.Id != id, ct))
        {
          slug = $"{originalSlug}-{counter++}";
        }

        category.Slug = slug;
      }

      // Validar parent si se especifica
      if (request.ParentId.HasValue)
      {
        if (request.ParentId.Value == id)
          throw new InvalidOperationException("Una categoría no puede ser su propio padre");

        var parentExists = await db.Categories.AnyAsync(c => c.Id == request.ParentId.Value, ct);
        if (!parentExists)
          throw new InvalidOperationException("La categoría padre no existe");
      }

      category.Name = request.Name;
      category.Description = request.Description;
      category.ImageUrl = request.ImageUrl;
      category.IsActive = request.IsActive;
      category.ParentId = request.ParentId;

      await db.SaveChangesAsync(ct);

      return await GetByIdAsync(category.Id, ct)
          ?? throw new InvalidOperationException("Error al recuperar la categoría actualizada");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("Tenant context not available");

      await using var db = _dbFactory.Create();

      var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
      if (category == null)
        throw new InvalidOperationException("Categoría no encontrada");

      // Desvincular productos asociados (solo quitar la relación)
      var productCategories = await db.ProductCategories
          .Where(pc => pc.CategoryId == id)
          .ToListAsync(ct);

      if (productCategories.Any())
      {
        db.ProductCategories.RemoveRange(productCategories);
      }

      // Desvincular subcategorías (preparado para futuro)
      var children = await db.Categories.Where(c => c.ParentId == id).ToListAsync(ct);
      foreach (var child in children)
      {
        child.ParentId = null;
      }

      // Eliminación física
      db.Categories.Remove(category);
      await db.SaveChangesAsync(ct);
    }

    public async Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("Tenant context not available");

      await using var db = _dbFactory.Create();

      var category = await db.Categories
          .Where(c => c.Id == id)
          .FirstOrDefaultAsync(ct);

      if (category == null)
        return null;

      // Contar productos asociados explícitamente
      var productCount = await db.ProductCategories
          .Where(pc => pc.CategoryId == id)
          .CountAsync(ct);

      return new CategoryResponse
      {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        Description = category.Description,
        ImageUrl = category.ImageUrl,
        IsActive = category.IsActive,
        ParentId = category.ParentId,
        ProductCount = productCount,
        CreatedAt = DateTime.MinValue,
        UpdatedAt = null
      };
    }

    public async Task<CategoryResponse?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("Tenant context not available");

      await using var db = _dbFactory.Create();

      var category = await db.Categories
          .Where(c => c.Slug == slug)
          .FirstOrDefaultAsync(ct);

      if (category == null)
        return null;

      // Contar productos asociados explícitamente
      var productCount = await db.ProductCategories
          .Where(pc => pc.CategoryId == category.Id)
          .CountAsync(ct);

      return new CategoryResponse
      {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        Description = category.Description,
        ImageUrl = category.ImageUrl,
        IsActive = category.IsActive,
        ParentId = category.ParentId,
        ProductCount = productCount,
        CreatedAt = DateTime.MinValue,
        UpdatedAt = null
      };
    }

    public async Task<CategoryListResponse> GetAllAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        CancellationToken ct = default)
    {
      if (!_tenantAccessor.HasTenant)
        throw new InvalidOperationException("Tenant context not available");

      await using var db = _dbFactory.Create();

      var query = db.Categories.AsQueryable();

      // Filtros
      if (!string.IsNullOrWhiteSpace(search))
      {
        var searchLower = search.ToLower();
        query = query.Where(c => c.Name.ToLower().Contains(searchLower) ||
                                (c.Description != null && c.Description.ToLower().Contains(searchLower)));
      }

      if (isActive.HasValue)
      {
        query = query.Where(c => c.IsActive == isActive.Value);
      }

      var total = await query.CountAsync(ct);

      var categories = await query
          .OrderBy(c => c.Name)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync(ct);

      // Contar productos para cada categoría
      var items = new List<CategoryListItem>();
      foreach (var cat in categories)
      {
        var productCount = await db.ProductCategories
            .Where(pc => pc.CategoryId == cat.Id)
            .CountAsync(ct);

        items.Add(new CategoryListItem
        {
          Id = cat.Id,
          Name = cat.Name,
          Slug = cat.Slug,
          ImageUrl = cat.ImageUrl,
          IsActive = cat.IsActive,
          ProductCount = productCount
        });
      }

      return new CategoryListResponse
      {
        Items = items,
        Total = total,
        Page = page,
        PageSize = pageSize
      };
    }

    /// <summary>
    /// Genera un slug URL-friendly a partir de un texto
    /// </summary>
    private static string GenerateSlug(string text)
    {
      // Convertir a minúsculas
      var slug = text.ToLowerInvariant();

      // Remover acentos
      slug = RemoveAccents(slug);

      // Reemplazar espacios y caracteres especiales con guiones
      slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
      slug = Regex.Replace(slug, @"\s+", "-");
      slug = Regex.Replace(slug, @"-+", "-");

      // Quitar guiones del inicio y fin
      slug = slug.Trim('-');

      return slug;
    }

    private static string RemoveAccents(string text)
    {
      var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
      var stringBuilder = new System.Text.StringBuilder();

      foreach (var c in normalizedString)
      {
        var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
        if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
        {
          stringBuilder.Append(c);
        }
      }

      return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
  }
}
