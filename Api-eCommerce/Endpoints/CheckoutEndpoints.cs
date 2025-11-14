using CC.Aplication.Catalog;
using CC.Aplication.Services;
using CC.Domain.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api_eCommerce.Endpoints
{
    public static class CheckoutEndpoints
    {
        public static IEndpointRouteBuilder MapCheckoutEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/checkout")
                .WithTags("Checkout");

            group.MapPost("/quote", GetQuote)
                .WithName("GetCheckoutQuote")
                .WithSummary("Obtiene un quote del pedido (totales, impuestos, envío)")
                .Produces<CheckoutQuoteResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized);

            group.MapPost("/place-order", PlaceOrder)
                .WithName("PlaceOrder")
                .WithSummary("Crea un pedido (requiere Idempotency-Key)")
                .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status409Conflict);

            return app;
        }

        private static async Task<IResult> GetQuote(
            HttpContext context,
            [FromBody] CheckoutQuoteRequest request,
            [FromServices] ICheckoutService checkoutService,
            [FromServices] IFeatureService featureService)
        {
            try
            {
                // Validar feature flags
                var validation = await ValidateCheckoutFeaturesAsync(context, featureService);
                if (validation != null) return validation;

                var sessionId = GetSessionId(context);
                var quote = await checkoutService.GetQuoteAsync(sessionId, request);
                return Results.Ok(quote);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }

        private static async Task<IResult> PlaceOrder(
            HttpContext context,
            [FromBody] PlaceOrderRequest request,
            [FromServices] ICheckoutService checkoutService,
            [FromServices] IFeatureService featureService)
        {
            try
            {
                // Validar feature flags
                var validation = await ValidateCheckoutFeaturesAsync(context, featureService);
                if (validation != null) return validation;

                // Si el método de pago es Wompi, validar que esté habilitado
                if (request.PaymentMethod?.ToLower() == "wompi")
                {
                    var wompiEnabled = await featureService.IsEnabledAsync(FeatureKeys.PaymentsWompiEnabled);
                    if (!wompiEnabled)
                    {
                        return Results.Problem(
                            detail: "El método de pago Wompi no está disponible para este tenant",
                            statusCode: StatusCodes.Status400BadRequest);
                    }
                }

                var sessionId = GetSessionId(context);
                // Obtener userId del JWT si está autenticado
                Guid? userId = GetUserIdFromJwt(context);

                var order = await checkoutService.PlaceOrderAsync(sessionId, request, userId);
                return Results.Created($"/api/orders/{order.OrderId}", order);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }

        /// <summary>
        /// Valida que el usuario tenga permiso para hacer checkout según los feature flags
        /// </summary>
        private static async Task<IResult?> ValidateCheckoutFeaturesAsync(
            HttpContext context, 
            IFeatureService featureService)
        {
            var allowGuestCheckout = await featureService.IsEnabledAsync(FeatureKeys.AllowGuestCheckout);
            var userId = GetUserIdFromJwt(context);
            var hasJwt = userId.HasValue;

            // Si allowGuestCheckout = false y no hay JWT ? 401
            if (!allowGuestCheckout && !hasJwt)
            {
                return Results.Unauthorized();
            }

            return null;
        }

        private static string GetSessionId(HttpContext context)
        {
            var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault();
            
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new InvalidOperationException("X-Session-Id header is required");
            }

            return sessionId;
        }

        /// <summary>
        /// Extrae el userId del JWT si está presente
        /// </summary>
        private static Guid? GetUserIdFromJwt(HttpContext context)
        {
            // Intentar obtener el claim del usuario autenticado
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? context.User.FindFirst("sub");
            
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
            
            return null;
        }
    }
}
