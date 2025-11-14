using CC.Aplication.Orders;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Api_eCommerce.Endpoints
{
    public static class OrdersEndpoints
    {
        public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/me/orders")
                .RequireAuthorization()
                .WithTags("User Orders");

            group.MapGet("", GetUserOrders)
                .WithName("GetUserOrders")
                .WithSummary("Get user's order history")
                .WithDescription("Returns a paginated list of orders for the authenticated user")
                .Produces<PagedOrdersResponse>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

            group.MapGet("/{orderId:guid}", GetOrderDetail)
                .WithName("GetOrderDetail")
                .WithSummary("Get order details")
                .WithDescription("Returns detailed information about a specific order")
                .Produces<OrderDetailDto>(StatusCodes.Status200OK)
                .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
                .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

            return app;
        }

        private static async Task<IResult> GetUserOrders(
            HttpContext context,
            IOrderService orderService,
            ITenantResolver tenantResolver,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
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
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirst("sub");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Crear query
                var query = new GetOrdersQuery(
                    Page: page,
                    PageSize: pageSize,
                    Status: status,
                    FromDate: fromDate,
                    ToDate: toDate
                );

                // Obtener órdenes
                var orders = await orderService.GetUserOrdersAsync(userId, query);
                return Results.Ok(orders);
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
                    detail: "An error occurred while retrieving orders"
                );
            }
        }

        private static async Task<IResult> GetOrderDetail(
            HttpContext context,
            Guid orderId,
            IOrderService orderService,
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
                var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirst("sub");

                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid Token",
                        detail: "User ID not found in token"
                    );
                }

                // Obtener detalle de la orden
                var order = await orderService.GetOrderDetailAsync(userId, orderId);

                if (order == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Order Not Found",
                        detail: $"Order {orderId} not found or does not belong to the user"
                    );
                }

                return Results.Ok(order);
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
                    detail: "An error occurred while retrieving order details"
                );
            }
        }
    }
}
