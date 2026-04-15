using Api_eCommerce.Authorization;
using CC.Aplication.Catalog;
using CC.Infraestructure.Tenancy;
using CC.Domain.Assets;
using CC.Domain.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para productos - Admin (CRUD completo con permisos)
  /// </summary>
  [ApiController]
  [Route("api/admin/products")]
  [Tags("Products - Admin")]
  [Authorize]
  public class ProductAdminController : ControllerBase
  {
    private readonly IProductService _productService;
    private readonly IAssetService _assetService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<ProductAdminController> _logger;

    public ProductAdminController(
        IProductService productService,
        IAssetService assetService,
        ITenantResolver tenantResolver,
        ILogger<ProductAdminController> logger)
    {
      _productService = productService;
      _assetService = assetService;
      _tenantResolver = tenantResolver;
      _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los productos con paginación y filtros
    /// </summary>
    /// <param name="filter">Filtros de búsqueda y paginación</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Lista paginada de productos</returns>
    [HttpGet]
    [RequireModule("catalog", "view")]
    [ProducesResponseType<PagedResult<ProductResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] ProductFilterDto filter,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant no resuelto",
              detail: "No se pudo resolver el tenant para la solicitud."
          );
        }

        var result = await _productService.GetPagedAsync(filter, ct);
        return Ok(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener productos paginados");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener productos",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por ID
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("{id:guid}")]
    [RequireModule("catalog", "view")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProductById(
        Guid id,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var product = await _productService.GetByIdAsync(id, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por slug (URL amigable)
    /// </summary>
    /// <param name="slug">Slug del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("slug/{slug}")]
    [RequireModule("catalog", "view")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetProductBySlug(
        string slug,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var product = await _productService.GetBySlugAsync(slug, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con slug '{slug}' no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto por slug {Slug}", slug);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Crea un nuevo producto
    /// </summary>
    /// <param name="dto">Datos del producto a crear</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto creado</returns>
    [HttpPost]
    [RequireModule("catalog", "create")]
    [Consumes("application/json")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductDto dto,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Validaciones básicas
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Name", new[] { "El nombre del producto es requerido" } }
                    }
          });
        }

        if (dto.Price <= 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Price", new[] { "El precio debe ser mayor a 0" } }
                    }
          });
        }

        var product = await _productService.CreateAsync(dto, ct);

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = product.Id },
            product
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al crear producto");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al crear producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Crea un nuevo producto con carga de media (imagen principal, galería y videos)
    /// </summary>
    [HttpPost]
    [RequireModule("catalog", "create")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateProductForm(
        [FromForm] CreateProductFormRequest request,
        CancellationToken ct)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error de validación",
            Errors = new Dictionary<string, string[]>
            {
              { "Name", new[] { "El nombre del producto es requerido" } }
            }
          });
        }

        if (request.Price <= 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error de validación",
            Errors = new Dictionary<string, string[]>
            {
              { "Price", new[] { "El precio debe ser mayor a 0" } }
            }
          });
        }

        var dto = new CreateProductDto
        {
          Name = request.Name,
          Sku = request.Sku,
          Description = request.Description,
          ShortDescription = request.ShortDescription,
          Price = request.Price,
          CompareAtPrice = request.CompareAtPrice,
          Stock = request.Stock,
          TrackInventory = request.TrackInventory,
          IsActive = request.IsActive,
          IsFeatured = request.IsFeatured,
          IsOnSale = request.IsOnSale,
          IsTaxIncluded = request.IsTaxIncluded,
          TaxPercentage = request.TaxPercentage,
          Tags = request.Tags,
          Brand = request.Brand,
          MetaTitle = request.MetaTitle,
          MetaDescription = request.MetaDescription,
          CategoryIds = request.CategoryIds,
          InitialStoreStock = request.InitialStoreStock
        };

        var created = await _productService.CreateAsync(dto, ct);
        var userId = ResolveUserId();

        var uploadedAssetIds = new List<Guid>();
        try
        {
          await UploadProductMediaAsync(
              created.Id,
              tenantContext.TenantId,
              userId,
              request.MainImage,
              request.Images,
              request.Videos,
              uploadedAssetIds,
              ct);
        }
        catch (Exception uploadEx)
        {
          await DeleteUploadedAssetsBestEffortAsync(tenantContext.TenantId, uploadedAssetIds, ct);
          await RollbackCreatedProductBestEffortAsync(created.Id, ct);
          _logger.LogWarning(uploadEx, "Upload de media fallido para producto {ProductId}. Se revirtió la creación.", created.Id);
          throw new InvalidOperationException(
              "No se pudo completar la carga de imagenes/videos del producto. Se intentó revertir la creación.",
              uploadEx);
        }

        var product = await _productService.GetByIdAsync(created.Id, ct) ?? created;

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = product.Id },
            product
        );
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Error de validación",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al crear producto por form-data");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al crear producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualiza un producto existente
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="dto">Datos a actualizar</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto actualizado</returns>
    [HttpPut("{id:guid}")]
    [RequireModule("catalog", "update")]
    [Consumes("application/json")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] UpdateProductDto dto,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant no resuelto",
              detail: "No se pudo resolver el tenant para la solicitud."
          );
        }

        // Validaciones
        if (dto.Price.HasValue && dto.Price <= 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Price", new[] { "El precio debe ser mayor a 0" } }
                    }
          });
        }

        var product = await _productService.UpdateAsync(id, dto, ct);
        return Ok(product);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Product Not Found",
          Detail = ex.Message
        });
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al actualizar producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al actualizar producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualiza un producto existente con carga de media
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequireModule("catalog", "update")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateProductForm(
        Guid id,
        [FromForm] UpdateProductFormRequest request,
        CancellationToken ct)
    {
      try
      {
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        if (request.Price.HasValue && request.Price <= 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error de validación",
            Errors = new Dictionary<string, string[]>
            {
              { "Price", new[] { "El precio debe ser mayor a 0" } }
            }
          });
        }

        var dto = new UpdateProductDto
        {
          Name = request.Name,
          Sku = request.Sku,
          Description = request.Description,
          ShortDescription = request.ShortDescription,
          Price = request.Price,
          CompareAtPrice = request.CompareAtPrice,
          Stock = request.Stock,
          TrackInventory = request.TrackInventory,
          IsActive = request.IsActive,
          IsFeatured = request.IsFeatured,
          IsOnSale = request.IsOnSale,
          IsTaxIncluded = request.IsTaxIncluded,
          TaxPercentage = request.TaxPercentage,
          Tags = request.Tags,
          Brand = request.Brand,
          MetaTitle = request.MetaTitle,
          MetaDescription = request.MetaDescription,
          CategoryIds = request.CategoryIds
        };

        var existingProduct = await _productService.GetByIdAsync(id, ct);
        if (existingProduct == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Producto no encontrado",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        var userId = ResolveUserId();
        var uploadedAssetIds = new List<Guid>();
        try
        {
          await UploadProductMediaAsync(
              id,
              tenantContext.TenantId,
              userId,
              request.MainImage,
              request.Images,
              request.Videos,
              uploadedAssetIds,
              ct);
        }
        catch (Exception uploadEx)
        {
          await DeleteUploadedAssetsBestEffortAsync(tenantContext.TenantId, uploadedAssetIds, ct);
          _logger.LogWarning(uploadEx, "Upload de media fallido para actualización de producto {ProductId}. Se revirtieron assets nuevos.", id);
          throw new InvalidOperationException(
              "No se pudo completar la carga de imagenes/videos del producto. La actualización no fue aplicada.",
              uploadEx);
        }

        try
        {
          await _productService.UpdateAsync(id, dto, ct);
        }
        catch
        {
          await DeleteUploadedAssetsBestEffortAsync(tenantContext.TenantId, uploadedAssetIds, ct);
          throw;
        }

        var product = await _productService.GetByIdAsync(id, ct);
        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Producto no encontrado",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return Ok(product);
      }
      catch (InvalidOperationException ex) when (
          ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
          ex.Message.Contains("no encontrado", StringComparison.OrdinalIgnoreCase))
      {
        return NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Producto no encontrado",
          Detail = "No se encontró el producto solicitado."
        });
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Error de validación",
            detail: ex.Message
        );
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al actualizar producto {ProductId} por form-data", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al actualizar producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Elimina un producto (soft delete)
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Sin contenido si fue exitoso</returns>
    [HttpDelete("{id:guid}")]
    [RequireModule("catalog", "delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteProduct(
        Guid id,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var deleted = await _productService.DeleteAsync(id, ct);

        if (!deleted)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al eliminar producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al eliminar producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Actualiza el stock de un producto
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="request">Nueva cantidad de stock</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Sin contenido si fue exitoso</returns>
    [HttpPatch("{id:guid}/stock")]
    [RequireModule("catalog", "update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateStock(
        Guid id,
        [FromBody] UpdateStockRequest request,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        if (request.Quantity < 0)
        {
          return ValidationProblem(new ValidationProblemDetails
          {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Errors = new Dictionary<string, string[]>
                    {
                        { "Quantity", new[] { "La cantidad no puede ser negativa" } }
                    }
          });
        }

        var updated = await _productService.UpdateStockAsync(id, request.Quantity, ct);

        if (!updated)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al actualizar stock del producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al actualizar stock",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Alterna el estado de producto destacado
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Sin contenido si fue exitoso</returns>
    [HttpPatch("{id:guid}/toggle-featured")]
    [RequireModule("catalog", "update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ToggleFeatured(
        Guid id,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var toggled = await _productService.ToggleFeaturedAsync(id, ct);

        if (!toggled)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al cambiar estado destacado del producto {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al cambiar estado destacado",
            detail: ex.Message
        );
      }
    }

    private async Task UploadProductMediaAsync(
        Guid productId,
        Guid tenantId,
        string userId,
        IFormFile? mainImage,
        List<IFormFile>? images,
        List<IFormFile>? videos,
        List<Guid> uploadedAssetIds,
        CancellationToken ct)
    {
      if (mainImage is { Length: > 0 })
      {
        uploadedAssetIds.Add(
            await UploadAssetAsync(productId, tenantId, userId, mainImage, TenantAssetType.Image, setAsPrimary: true, ct));
      }

      if (images is { Count: > 0 })
      {
        foreach (var image in images.Where(f => f is { Length: > 0 }))
        {
          uploadedAssetIds.Add(
              await UploadAssetAsync(productId, tenantId, userId, image, TenantAssetType.Image, setAsPrimary: false, ct));
        }
      }

      if (videos is { Count: > 0 })
      {
        foreach (var video in videos.Where(f => f is { Length: > 0 }))
        {
          uploadedAssetIds.Add(
              await UploadAssetAsync(productId, tenantId, userId, video, TenantAssetType.Video, setAsPrimary: false, ct));
        }
      }
    }

    private async Task<Guid> UploadAssetAsync(
        Guid productId,
        Guid tenantId,
        string userId,
        IFormFile file,
        TenantAssetType assetType,
        bool setAsPrimary,
        CancellationToken ct)
    {
      await using var stream = file.OpenReadStream();

      var uploaded = await _assetService.UploadAsync(new UploadAssetCommand
      {
        TenantId = tenantId,
        UploadedByUserId = userId,
        Module = "product",
        EntityType = "product",
        EntityId = productId.ToString(),
        AssetType = assetType,
        Visibility = TenantAssetVisibility.Public,
        OriginalFileName = file.FileName,
        ContentType = file.ContentType,
        SizeBytes = file.Length,
        Content = stream,
        SetAsPrimary = setAsPrimary
      }, ct);

      return uploaded.Id;
    }

    private async Task DeleteUploadedAssetsBestEffortAsync(Guid tenantId, IEnumerable<Guid> assetIds, CancellationToken ct)
    {
      foreach (var assetId in assetIds)
      {
        try
        {
          await _assetService.DeleteSingleAsync(tenantId, assetId, ct);
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "No fue posible revertir asset {AssetId} para tenant {TenantId}", assetId, tenantId);
        }
      }
    }

    private async Task RollbackCreatedProductBestEffortAsync(Guid productId, CancellationToken ct)
    {
      try
      {
        await _productService.DeleteAsync(productId, ct);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "No fue posible revertir el producto {ProductId} tras fallo de upload", productId);
      }
    }

    private string ResolveUserId()
    {
      return User.FindFirst("sub")?.Value
        ?? User.FindFirst("id")?.Value
        ?? User.FindFirst("email")?.Value
        ?? "system";
    }
  }

  public sealed class CreateProductFormRequest
  {
    [FromForm(Name = "name")]
    public string Name { get; init; } = string.Empty;

    [FromForm(Name = "sku")]
    public string? Sku { get; init; }

    [FromForm(Name = "description")]
    public string? Description { get; init; }

    [FromForm(Name = "shortDescription")]
    public string? ShortDescription { get; init; }

    [FromForm(Name = "price")]
    public decimal Price { get; init; }

    [FromForm(Name = "compareAtPrice")]
    public decimal? CompareAtPrice { get; init; }

    [FromForm(Name = "stock")]
    public int Stock { get; init; } = 0;

    [FromForm(Name = "trackInventory")]
    public bool TrackInventory { get; init; } = true;

    [FromForm(Name = "isActive")]
    public bool IsActive { get; init; } = true;

    [FromForm(Name = "isFeatured")]
    public bool IsFeatured { get; init; }

    [FromForm(Name = "isOnSale")]
    public bool IsOnSale { get; init; }

    [FromForm(Name = "isTaxIncluded")]
    public bool IsTaxIncluded { get; init; }

    [FromForm(Name = "taxPercentage")]
    public decimal? TaxPercentage { get; init; }

    [FromForm(Name = "tags")]
    public string? Tags { get; init; }

    [FromForm(Name = "brand")]
    public string? Brand { get; init; }

    [FromForm(Name = "metaTitle")]
    public string? MetaTitle { get; init; }

    [FromForm(Name = "metaDescription")]
    public string? MetaDescription { get; init; }

    [FromForm(Name = "categoryIds")]
    public List<Guid>? CategoryIds { get; init; }

    [FromForm(Name = "mainImage")]
    public IFormFile? MainImage { get; init; }

    [FromForm(Name = "images")]
    public List<IFormFile>? Images { get; init; }

    [FromForm(Name = "videos")]
    public List<IFormFile>? Videos { get; init; }

    [FromForm(Name = "initialStoreStock")]
    public List<InitialStoreStockDto>? InitialStoreStock { get; init; }
  }

  public sealed class UpdateProductFormRequest
  {
    [FromForm(Name = "name")]
    public string? Name { get; init; }

    [FromForm(Name = "sku")]
    public string? Sku { get; init; }

    [FromForm(Name = "description")]
    public string? Description { get; init; }

    [FromForm(Name = "shortDescription")]
    public string? ShortDescription { get; init; }

    [FromForm(Name = "price")]
    public decimal? Price { get; init; }

    [FromForm(Name = "compareAtPrice")]
    public decimal? CompareAtPrice { get; init; }

    [FromForm(Name = "stock")]
    public int? Stock { get; init; }

    [FromForm(Name = "trackInventory")]
    public bool? TrackInventory { get; init; }

    [FromForm(Name = "isActive")]
    public bool? IsActive { get; init; }

    [FromForm(Name = "isFeatured")]
    public bool? IsFeatured { get; init; }

    [FromForm(Name = "isOnSale")]
    public bool? IsOnSale { get; init; }

    [FromForm(Name = "isTaxIncluded")]
    public bool? IsTaxIncluded { get; init; }

    [FromForm(Name = "taxPercentage")]
    public decimal? TaxPercentage { get; init; }

    [FromForm(Name = "tags")]
    public string? Tags { get; init; }

    [FromForm(Name = "brand")]
    public string? Brand { get; init; }

    [FromForm(Name = "metaTitle")]
    public string? MetaTitle { get; init; }

    [FromForm(Name = "metaDescription")]
    public string? MetaDescription { get; init; }

    [FromForm(Name = "categoryIds")]
    public List<Guid>? CategoryIds { get; init; }

    [FromForm(Name = "mainImage")]
    public IFormFile? MainImage { get; init; }

    [FromForm(Name = "images")]
    public List<IFormFile>? Images { get; init; }

    [FromForm(Name = "videos")]
    public List<IFormFile>? Videos { get; init; }
  }

  // ==================== CONTROLLER PÚBLICO ====================

  /// <summary>
  /// Controlador público de productos para storefront (sin autenticación)
  /// </summary>
  [ApiController]
  [Route("api/products")]
  [Tags("Products - Public")]
  public class ProductController : ControllerBase
  {
    private readonly IProductService _productService;
    private readonly ITenantResolver _tenantResolver;
    private readonly ILogger<ProductController> _logger;

    public ProductController(
        IProductService productService,
        ITenantResolver tenantResolver,
        ILogger<ProductController> logger)
    {
      _productService = productService;
      _tenantResolver = tenantResolver;
      _logger = logger;
    }

    /// <summary>
    /// Obtiene todos los productos (público - para storefront)
    /// </summary>
    /// <param name="filter">Filtros de búsqueda y paginación</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Lista paginada de productos</returns>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ProductResponseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPublicProducts(
        [FromQuery] ProductFilterDto filter,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var result = await _productService.GetPagedAsync(filter, ct);
        return Ok(result);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener productos públicos");
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener productos",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por ID (público)
    /// </summary>
    /// <param name="id">ID del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPublicProductById(
        Guid id,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var product = await _productService.GetByIdAsync(id, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con ID {id} no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto público {ProductId}", id);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }

    /// <summary>
    /// Obtiene un producto por slug (público - para URLs amigables)
    /// </summary>
    /// <param name="slug">Slug del producto</param>
    /// <param name="ct">Token de cancelación</param>
    /// <returns>Producto encontrado</returns>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPublicProductBySlug(
        string slug,
        CancellationToken ct)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        var product = await _productService.GetBySlugAsync(slug, ct);

        if (product == null)
        {
          return NotFound(new ProblemDetails
          {
            Status = StatusCodes.Status404NotFound,
            Title = "Product Not Found",
            Detail = $"Producto con slug '{slug}' no encontrado"
          });
        }

        return Ok(product);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error al obtener producto público por slug {Slug}", slug);
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Error al obtener producto",
            detail: ex.Message
        );
      }
    }
  }
}
