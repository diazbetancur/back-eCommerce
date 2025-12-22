using CC.Aplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para gestión del carrito de compras
  /// </summary>
  [ApiController]
  [Route("api/cart")]
  [Tags("Shopping Cart")]
  public class CartController : ControllerBase
  {
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
      _cartService = cartService;
    }

    /// <summary>
    /// Obtiene el carrito actual (por X-Session-Id)
    /// </summary>
    [HttpGet]
    [ProducesResponseType<CC.Aplication.Catalog.CartDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCart()
    {
      var sessionId = GetSessionId();
      var cart = await _cartService.GetOrCreateCartAsync(sessionId);
      return Ok(cart);
    }

    /// <summary>
    /// Agrega un producto al carrito
    /// </summary>
    [HttpPost("items")]
    [ProducesResponseType<CC.Aplication.Catalog.CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddToCart([FromBody] CC.Aplication.Catalog.AddToCartRequest request)
    {
      try
      {
        var sessionId = GetSessionId();
        var cart = await _cartService.AddToCartAsync(sessionId, request);
        return Ok(cart);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
      }
    }

    /// <summary>
    /// Actualiza la cantidad de un item del carrito
    /// </summary>
    [HttpPut("items/{itemId:guid}")]
    [ProducesResponseType<CC.Aplication.Catalog.CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] CC.Aplication.Catalog.UpdateCartItemRequest request)
    {
      try
      {
        var sessionId = GetSessionId();
        var cart = await _cartService.UpdateCartItemAsync(sessionId, itemId, request);
        return Ok(cart);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
      }
    }

    /// <summary>
    /// Elimina un item del carrito
    /// </summary>
    [HttpDelete("items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCartItem(Guid itemId)
    {
      var sessionId = GetSessionId();
      var removed = await _cartService.RemoveCartItemAsync(sessionId, itemId);
      return removed ? NoContent() : NotFound();
    }

    /// <summary>
    /// Vacía el carrito
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearCart()
    {
      var sessionId = GetSessionId();
      await _cartService.ClearCartAsync(sessionId);
      return NoContent();
    }

    private string GetSessionId()
    {
      // Intentar obtener de header
      var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault();

      if (string.IsNullOrWhiteSpace(sessionId))
      {
        // Generar nuevo session ID si no existe
        sessionId = Guid.NewGuid().ToString("N");
        Response.Headers.Append("X-Session-Id", sessionId);
      }

      return sessionId;
    }
  }
}
