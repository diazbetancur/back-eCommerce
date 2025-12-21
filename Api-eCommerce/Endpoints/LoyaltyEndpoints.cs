using CC.Aplication.Loyalty;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Api_eCommerce.Endpoints
{
    public static class LoyaltyEndpoints
    {
        public static IEndpointRouteBuilder MapLoyaltyEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/me/loyalty")
                .RequireAuthorization()
                .WithTags("User Loyalty");

            group.MapGet("", GetLoyalty)
                .WithName("GetLoyalty")
                .WithSummary("Get user's loyalty account")
                .WithDescription("Returns loyalty balance, total earned, total redeemed, and last transactions")
                .Produces<LoyaltyAccountSummaryDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            group.MapGet("/transactions", GetTransactions)
                .WithName("GetLoyaltyTransactions")
                .WithSummary("Get user's loyalty transactions")
                .WithDescription("Returns paginated list of loyalty point transactions")
                .Produces<PagedLoyaltyTransactionsResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            return app;
        }

        private static async Task<IResult> GetLoyalty(
            HttpContext context,
            ILoyaltyService loyaltyService,
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

                // Obtener cuenta de loyalty
                var loyalty = await loyaltyService.GetUserLoyaltyAsync(userId.Value);
                return Results.Ok(loyalty);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving loyalty information"
                );
            }
        }

        private static async Task<IResult> GetTransactions(
            HttpContext context,
            ILoyaltyService loyaltyService,
            ITenantResolver tenantResolver,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? type = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
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

                // Crear query
                var query = new GetLoyaltyTransactionsQuery(
                    Page: page,
                    PageSize: pageSize,
                    Type: type,
                    FromDate: fromDate,
                    ToDate: toDate
                );

                // Obtener transacciones
                var transactions = await loyaltyService.GetUserTransactionsAsync(userId.Value, query);
                return Results.Ok(transactions);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Operation Failed",
                    detail: ex.Message
                );
            }
            catch (Exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving transactions"
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
}
