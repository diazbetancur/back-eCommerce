using CC.Aplication.Catalog;
using CC.Aplication.Services;
using CC.Domain.Features;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Controlador para proceso de checkout y generación de órdenes
  /// </summary>
  [ApiController]
  [Route("api/checkout")]
  [Tags("Checkout")]
  public class CheckoutController : ControllerBase
  {
    private readonly ICheckoutService _checkoutService;
    private readonly IFeatureService _featureService;

    public CheckoutController(ICheckoutService checkoutService, IFeatureService featureService)
    {
      _checkoutService = checkoutService;
      _featureService = featureService;
    }

    /// <summary>
    /// Obtiene un quote del pedido (totales, impuestos, envío)
    /// </summary>
    [HttpPost("quote")]
    [ProducesResponseType<CheckoutQuoteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetQuote([FromBody] CheckoutQuoteRequest request)
    {
      try
      {
        // Validar feature flags
        var validation = await ValidateCheckoutFeaturesAsync();
        if (validation != null) return validation;

        var sessionId = GetSessionId();
        var quote = await _checkoutService.GetQuoteAsync(sessionId, request);
        return Ok(quote);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
      }
    }

    /// <summary>
    /// Crea un pedido (requiere Idempotency-Key)
    /// </summary>
    [HttpPost("place-order")]
    [ProducesResponseType<PlaceOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
      try
      {
        // Validar feature flags
        var validation = await ValidateCheckoutFeaturesAsync();
        if (validation != null) return validation;

        // Si el método de pago es Wompi, validar que esté habilitado
        if (request.PaymentMethod?.ToLower() == "wompi")
        {
          var wompiEnabled = await _featureService.IsEnabledAsync(FeatureKeys.PaymentsWompiEnabled);
          if (!wompiEnabled)
          {
            return Problem(
                detail: "El método de pago Wompi no está disponible para este tenant",
                statusCode: StatusCodes.Status400BadRequest);
          }
        }

        var sessionId = GetSessionId();
        // Obtener userId del JWT si está autenticado
        Guid? userId = GetUserIdFromJwt();

        var order = await _checkoutService.PlaceOrderAsync(sessionId, request, userId);
        return CreatedAtAction("GetOrderDetail", "Orders", new { orderId = order.OrderId }, order);
      }
      catch (InvalidOperationException ex)
      {
        return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
      }
    }

    /// <summary>
    /// Valida que el usuario tenga permiso para hacer checkout según los feature flags
    /// </summary>
    private async Task<IActionResult?> ValidateCheckoutFeaturesAsync()
    {
      var allowGuestCheckout = await _featureService.IsEnabledAsync(FeatureKeys.AllowGuestCheckout);
      var userId = GetUserIdFromJwt();
      var hasJwt = userId.HasValue;

      // Si allowGuestCheckout = false y no hay JWT → 401
      if (!allowGuestCheckout && !hasJwt)
      {
        return Unauthorized();
      }

      return null;
    }

    private string GetSessionId()
    {
      var sessionId = Request.Headers["X-Session-Id"].FirstOrDefault();

      if (string.IsNullOrWhiteSpace(sessionId))
      {
        throw new InvalidOperationException("X-Session-Id header is required");
      }

      return sessionId;
    }

    /// <summary>
    /// Extrae el userId del JWT si está presente
    /// </summary>
    private Guid? GetUserIdFromJwt()
    {
      // Intentar obtener el claim del usuario autenticado
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
          ?? User.FindFirst("sub");

      if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
      {
        return userId;
      }

      return null;
    }
  }
}
