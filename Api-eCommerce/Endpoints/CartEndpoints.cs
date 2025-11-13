using CC.Aplication.Catalog;
using CC.Aplication.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Api_eCommerce.Endpoints
{
    public static class CartEndpoints
    {
        public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/cart")
                .WithTags("Shopping Cart");

            group.MapGet("", GetCart)
                .WithName("GetCart")
                .WithSummary("Obtiene el carrito actual (por X-Session-Id)")
                .Produces<CartDto>(StatusCodes.Status200OK);

            group.MapPost("/items", AddToCart)
                .WithName("AddToCart")
                .WithSummary("Agrega un producto al carrito")
                .Produces<CartDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            group.MapPut("/items/{itemId}", UpdateCartItem)
                .WithName("UpdateCartItem")
                .WithSummary("Actualiza la cantidad de un item del carrito")
                .Produces<CartDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            group.MapDelete("/items/{itemId}", RemoveCartItem)
                .WithName("RemoveCartItem")
                .WithSummary("Elimina un item del carrito")
                .Produces(StatusCodes.Status204NoContent)
                .Produces(StatusCodes.Status404NotFound);

            group.MapDelete("", ClearCart)
                .WithName("ClearCart")
                .WithSummary("Vacía el carrito")
                .Produces(StatusCodes.Status204NoContent);

            return app;
        }

        private static async Task<IResult> GetCart(
            HttpContext context,
            [FromServices] ICartService cartService)
        {
            var sessionId = GetSessionId(context);
            var cart = await cartService.GetOrCreateCartAsync(sessionId);
            return Results.Ok(cart);
        }

        private static async Task<IResult> AddToCart(
            HttpContext context,
            [FromBody] AddToCartRequest request,
            [FromServices] ICartService cartService)
        {
            try
            {
                var sessionId = GetSessionId(context);
                var cart = await cartService.AddToCartAsync(sessionId, request);
                return Results.Ok(cart);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }

        private static async Task<IResult> UpdateCartItem(
            HttpContext context,
            Guid itemId,
            [FromBody] UpdateCartItemRequest request,
            [FromServices] ICartService cartService)
        {
            try
            {
                var sessionId = GetSessionId(context);
                var cart = await cartService.UpdateCartItemAsync(sessionId, itemId, request);
                return Results.Ok(cart);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
            }
        }

        private static async Task<IResult> RemoveCartItem(
            HttpContext context,
            Guid itemId,
            [FromServices] ICartService cartService)
        {
            var sessionId = GetSessionId(context);
            var removed = await cartService.RemoveCartItemAsync(sessionId, itemId);
            return removed ? Results.NoContent() : Results.NotFound();
        }

        private static async Task<IResult> ClearCart(
            HttpContext context,
            [FromServices] ICartService cartService)
        {
            var sessionId = GetSessionId(context);
            await cartService.ClearCartAsync(sessionId);
            return Results.NoContent();
        }

        private static string GetSessionId(HttpContext context)
        {
            // Intentar obtener de header
            var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault();
            
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                // Generar nuevo session ID si no existe
                sessionId = Guid.NewGuid().ToString("N");
                context.Response.Headers.Append("X-Session-Id", sessionId);
            }

            return sessionId;
        }
    }
}
