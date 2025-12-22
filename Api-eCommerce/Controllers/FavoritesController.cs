using CC.Aplication.Favorites;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para gestión de favoritos de usuario
  /// </summary>
  [ApiController]
  [Route("me/favorites")]
  [Authorize]
  [Tags("User Favorites")]
  public class FavoritesController : ControllerBase
  {
    private readonly IFavoritesService _favoritesService;
    private readonly ITenantResolver _tenantResolver;

    public FavoritesController(IFavoritesService favoritesService, ITenantResolver tenantResolver)
    {
      _favoritesService = favoritesService;
      _tenantResolver = tenantResolver;
    }

    /// <summary>
    /// Get user's favorite products
    /// </summary>
    [HttpGet]
    [ProducesResponseType<FavoriteListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFavorites()
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Obtener user ID del token JWT
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue)
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Obtener favoritos
        var favorites = await _favoritesService.GetUserFavoritesAsync(userId.Value);
        return Ok(favorites);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Operation Failed",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while retrieving favorites"
        );
      }
    }

    /// <summary>
    /// Add product to favorites
    /// </summary>
    [HttpPost]
    [ProducesResponseType<AddFavoriteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteRequest request)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Obtener user ID del token JWT
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue)
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Validar request
        if (request.ProductId == Guid.Empty)
        {
          return Problem(
              statusCode: StatusCodes.Status400BadRequest,
              title: "Validation Error",
              detail: "ProductId is required and must be a valid GUID"
          );
        }

        // Agregar favorito
        var response = await _favoritesService.AddFavoriteAsync(userId.Value, request.ProductId);
        return Ok(response);
      }
      catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
      {
        return Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Product Not Found",
            detail: ex.Message
        );
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Operation Failed",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while adding favorite"
        );
      }
    }

    /// <summary>
    /// Remove product from favorites
    /// </summary>
    [HttpDelete("{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFavorite(Guid productId)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Obtener user ID del token JWT
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue)
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Eliminar favorito
        var removed = await _favoritesService.RemoveFavoriteAsync(userId.Value, productId);

        if (!removed)
        {
          return Problem(
              statusCode: StatusCodes.Status404NotFound,
              title: "Favorite Not Found",
              detail: $"Product {productId} is not in favorites"
          );
        }

        return NoContent();
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Operation Failed",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while removing favorite"
        );
      }
    }

    /// <summary>
    /// Check if product is favorite
    /// </summary>
    [HttpGet("check/{productId:guid}")]
    [ProducesResponseType<CheckFavoriteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckFavorite(Guid productId)
    {
      try
      {
        // Validar tenant
        var tenantContext = await _tenantResolver.ResolveAsync(HttpContext);
        if (tenantContext == null)
        {
          return Problem(
              statusCode: StatusCodes.Status409Conflict,
              title: "Tenant Not Resolved",
              detail: "Unable to resolve tenant from request"
          );
        }

        // Obtener user ID del token JWT
        var userId = GetUserIdFromJwt();
        if (!userId.HasValue)
        {
          return Problem(
              statusCode: StatusCodes.Status401Unauthorized,
              title: "Invalid Token",
              detail: "User ID not found in token"
          );
        }

        // Verificar si es favorito
        var isFavorite = await _favoritesService.IsFavoriteAsync(userId.Value, productId);
        return Ok(new CheckFavoriteResponse(isFavorite));
      }
      catch (InvalidOperationException ex)
      {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Operation Failed",
            detail: ex.Message
        );
      }
      catch (Exception)
      {
        return Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An error occurred while checking favorite"
        );
      }
    }

    private Guid? GetUserIdFromJwt()
    {
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
          ?? User.FindFirst("sub");

      if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
      {
        return userId;
      }

      return null;
    }
  }

  /// <summary>
  /// Response DTO para check endpoint
  /// </summary>
  public record CheckFavoriteResponse(bool IsFavorite);
}
