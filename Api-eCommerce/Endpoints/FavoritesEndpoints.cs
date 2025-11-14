using CC.Aplication.Favorites;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Api_eCommerce.Endpoints
{
    public static class FavoritesEndpoints
    {
        public static IEndpointRouteBuilder MapFavoritesEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/me/favorites")
                .RequireAuthorization()
                .WithTags("User Favorites");

            group.MapGet("", GetFavorites)
                .WithName("GetFavorites")
                .WithSummary("Get user's favorite products")
                .WithDescription("Returns list of products marked as favorites by the authenticated user")
                .Produces<FavoriteListResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            group.MapPost("", AddFavorite)
                .WithName("AddFavorite")
                .WithSummary("Add product to favorites")
                .WithDescription("Marks a product as favorite (idempotent operation)")
                .Produces<AddFavoriteResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            group.MapDelete("/{productId:guid}", RemoveFavorite)
                .WithName("RemoveFavorite")
                .WithSummary("Remove product from favorites")
                .WithDescription("Removes a product from user's favorites")
                .Produces(StatusCodes.Status204NoContent)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            group.MapGet("/check/{productId:guid}", CheckFavorite)
                .WithName("CheckFavorite")
                .WithSummary("Check if product is favorite")
                .WithDescription("Returns true if the product is in user's favorites")
                .Produces<CheckFavoriteResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            return app;
        }

        private static async Task<IResult> GetFavorites(
            HttpContext context,
            IFavoritesService favoritesService,
            ITenantResolver tenantResolver)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Obtener user ID del token JWT
                var userId = GetUserIdFromJwt(context);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Obtener favoritos
                var favorites = await favoritesService.GetUserFavoritesAsync(userId.Value);
                return Results.Ok(favorites);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving favorites"
                );
            }
        }

        private static async Task<IResult> AddFavorite(
            HttpContext context,
            [FromBody] AddFavoriteRequest request,
            IFavoritesService favoritesService,
            ITenantResolver tenantResolver)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Obtener user ID del token JWT
                var userId = GetUserIdFromJwt(context);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Validar request
                if (request.ProductId == Guid.Empty)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Validation Error",
                        detail: "ProductId is required and must be a valid GUID"
                    );
                }

                // Agregar favorito
                var response = await favoritesService.AddFavoriteAsync(userId.Value, request.ProductId);
                return Results.Ok(response);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Product Not Found",
                    detail: ex.Message
                );
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while adding favorite"
                );
            }
        }

        private static async Task<IResult> RemoveFavorite(
            HttpContext context,
            Guid productId,
            IFavoritesService favoritesService,
            ITenantResolver tenantResolver)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Obtener user ID del token JWT
                var userId = GetUserIdFromJwt(context);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Eliminar favorito
                var removed = await favoritesService.RemoveFavoriteAsync(userId.Value, productId);
                
                if (!removed)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Favorite Not Found",
                        detail: $"Product {productId} is not in favorites"
                    );
                }

                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while removing favorite"
                );
            }
        }

        private static async Task<IResult> CheckFavorite(
            HttpContext context,
            Guid productId,
            IFavoritesService favoritesService,
            ITenantResolver tenantResolver)
        {
            try
            {
                // Validar tenant
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Tenant Not Resolved",
                        detail: "Unable to resolve tenant from request"
                    );
                }

                // Obtener user ID del token JWT
                var userId = GetUserIdFromJwt(context);
                if (!userId.HasValue)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Verificar si es favorito
                var isFavorite = await favoritesService.IsFavoriteAsync(userId.Value, productId);
                return Results.Ok(new CheckFavoriteResponse(isFavorite));
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while checking favorite"
                );
            }
        }

        private static Guid? GetUserIdFromJwt(HttpContext context)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirst("sub");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
    }

    // Response DTO para check endpoint
    public record CheckFavoriteResponse(bool IsFavorite);
}
