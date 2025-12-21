using Api_eCommerce.Authorization;
using CC.Infraestructure.Tenancy;
using CC.Infraestructure.Tenant;
using CC.Infraestructure.Tenant.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api_eCommerce.Endpoints
{
    /// <summary>
    /// Endpoints de administraci�n del tenant (requiere X-Tenant-Slug y autenticaci�n)
    /// Acceso basado en permisos de m�dulos
    /// </summary>
    public static class TenantAdminEndpoints
    {
        public static IEndpointRouteBuilder MapTenantAdminEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/admin")
                .RequireAuthorization()
                .AddEndpointFilter<ModuleAuthorizationFilter>()
                .WithTags("Tenant Admin");

            // ==================== PRODUCTS (M�dulo: inventory) ====================
            group.MapGet("/products", GetProducts)
                .WithName("AdminGetProducts")
                .WithSummary("Get products (Admin)")
                .WithMetadata(new RequireModuleAttribute("inventory", "view"))
                .Produces<AdminProductsResponse>(StatusCodes.Status200OK);

            group.MapGet("/products/{id:guid}", GetProductById)
                .WithName("AdminGetProductById")
                .WithSummary("Get product by ID (Admin)")
                .WithMetadata(new RequireModuleAttribute("inventory", "view"))
                .Produces<ProductDetailDto>(StatusCodes.Status200OK);

            group.MapPost("/products", CreateProduct)
                .WithName("AdminCreateProduct")
                .WithSummary("Create product (Admin)")
                .WithMetadata(new RequireModuleAttribute("inventory", "create"))
                .Produces<ProductDetailDto>(StatusCodes.Status201Created);

            group.MapPut("/products/{id:guid}", UpdateProduct)
                .WithName("AdminUpdateProduct")
                .WithSummary("Update product (Admin)")
                .WithMetadata(new RequireModuleAttribute("inventory", "update"))
                .Produces<ProductDetailDto>(StatusCodes.Status200OK);

            group.MapDelete("/products/{id:guid}", DeleteProduct)
                .WithName("AdminDeleteProduct")
                .WithSummary("Delete product (Admin)")
                .WithMetadata(new RequireModuleAttribute("inventory", "delete"))
                .Produces(StatusCodes.Status204NoContent);

            // ==================== ORDERS (M�dulo: sales) ====================
            group.MapGet("/orders", GetOrders)
                .WithName("AdminGetOrders")
                .WithSummary("Get all orders (Admin)")
                .WithMetadata(new RequireModuleAttribute("sales", "view"))
                .Produces<AdminOrdersResponse>(StatusCodes.Status200OK);

            group.MapGet("/orders/{id:guid}", GetOrderById)
                .WithName("AdminGetOrderById")
                .WithSummary("Get order by ID (Admin)")
                .WithMetadata(new RequireModuleAttribute("sales", "view"))
                .Produces<AdminOrderDetailDto>(StatusCodes.Status200OK);

            group.MapPatch("/orders/{id:guid}/status", UpdateOrderStatus)
                .WithName("AdminUpdateOrderStatus")
                .WithSummary("Update order status (Admin)")
                .WithMetadata(new RequireModuleAttribute("sales", "update"))
                .Produces<AdminOrderDetailDto>(StatusCodes.Status200OK);

            // ==================== USERS (M�dulo: customers) ====================
            group.MapGet("/users", GetUsers)
                .WithName("AdminGetUsers")
                .WithSummary("Get tenant users (Admin)")
                .WithMetadata(new RequireModuleAttribute("customers", "view"))
                .Produces<AdminUsersResponse>(StatusCodes.Status200OK);

            group.MapPost("/users", CreateUser)
                .WithName("AdminCreateUser")
                .WithSummary("Create tenant user (Admin)")
                .WithMetadata(new RequireModuleAttribute("customers", "create"))
                .Produces<TenantUserDetailDto>(StatusCodes.Status201Created);

            group.MapPatch("/users/{id:guid}/role", AssignRole)
                .WithName("AdminAssignRole")
                .WithSummary("Assign role to user (Admin)")
                .WithMetadata(new RequireModuleAttribute("customers", "update"))
                .Produces<TenantUserDetailDto>(StatusCodes.Status200OK);

            // ==================== PLAN STATUS ====================
            group.MapGet("/plan/status", GetPlanStatus)
                .WithName("AdminGetPlanStatus")
                .WithSummary("Get current plan limits and usage status")
                .WithDescription("Returns the current plan, limits, usage and whether any limits are exceeded. Useful for dashboard and before creating/editing resources.")
                .Produces<CC.Aplication.Plans.PlanStatusDto>(StatusCodes.Status200OK);

            return group;
        }

        // ==================== PLAN STATUS HANDLER ====================

        private static async Task<IResult> GetPlanStatus(
            HttpContext context,
            CC.Aplication.Plans.IPlanLimitService planLimitService,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                var planStatus = await planLimitService.GetPlanStatusAsync();
                return Results.Ok(planStatus);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Plan Status Error",
                    detail: ex.Message
                );
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Internal Server Error",
                    detail: "An error occurred while retrieving plan status"
                );
            }
        }

        // ==================== PRODUCTS HANDLERS ====================

        private static async Task<IResult> GetProducts(
            HttpContext context,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var query = db.Products.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
                }

                var totalCount = await query.CountAsync();
                var products = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new ProductListItemDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Price = p.Price,
                        Stock = p.Stock,
                        IsActive = p.IsActive,
                        CreatedAt = p.CreatedAt
                    })
                    .ToListAsync();

                return Results.Ok(new AdminProductsResponse
                {
                    Products = products,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return InternalServerError("retrieving products");
            }
        }

        private static async Task<IResult> GetProductById(
            HttpContext context,
            Guid id,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var product = await db.Products
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    return Results.NotFound(new { error = "Product not found" });
                }

                var dto = new ProductDetailDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Stock = product.Stock,
                    IsActive = product.IsActive,
                    CreatedAt = product.CreatedAt
                };

                return Results.Ok(dto);
            }
            catch (Exception ex)
            {
                return InternalServerError("retrieving product");
            }
        }

        private static async Task<IResult> CreateProduct(
            HttpContext context,
            [FromBody] AdminCreateProductRequest request,  // ? ACTUALIZADO
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver,
            CC.Aplication.Plans.IPlanLimitService planLimitService)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                // ? VALIDAR L�MITE DE PRODUCTOS
                var currentProductCount = await db.Products.CountAsync();
                await planLimitService.ThrowIfExceedsLimitAsync(
                    CC.Infraestructure.Admin.Entities.PlanLimitCodes.MaxProducts,
                    currentProductCount,
                    "Has alcanzado el l�mite de productos de tu plan. Actualiza tu plan para agregar m�s productos."
                );

                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    Price = request.Price,
                    Stock = request.Stock,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                db.Products.Add(product);
                await db.SaveChangesAsync();

                var dto = new ProductDetailDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Stock = product.Stock,
                    IsActive = product.IsActive,
                    CreatedAt = product.CreatedAt
                };

                return Results.Created($"/admin/products/{product.Id}", dto);
            }
            catch (CC.Aplication.Plans.PlanLimitExceededException ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status402PaymentRequired,
                    title: "Plan Limit Exceeded",
                    detail: ex.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        { "limitCode", ex.LimitCode },
                        { "limitValue", ex.LimitValue },
                        { "currentValue", ex.CurrentValue }
                    }
                );
            }
            catch (Exception ex)
            {
                return InternalServerError("creating product");
            }
        }

        private static async Task<IResult> UpdateProduct(
            HttpContext context,
            Guid id,
            [FromBody] AdminUpdateProductRequest request,  // ? ACTUALIZADO
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var product = await db.Products.FindAsync(id);
                if (product == null)
                {
                    return Results.NotFound(new { error = "Product not found" });
                }

                product.Name = request.Name ?? product.Name;
                product.Description = request.Description ?? product.Description;
                product.Price = request.Price ?? product.Price;
                product.Stock = request.Stock ?? product.Stock;
                product.IsActive = request.IsActive ?? product.IsActive;

                await db.SaveChangesAsync();

                var dto = new ProductDetailDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Stock = product.Stock,
                    IsActive = product.IsActive,
                    CreatedAt = product.CreatedAt
                };

                return Results.Ok(dto);
            }
            catch (Exception ex)
            {
                return InternalServerError("updating product");
            }
        }

        private static async Task<IResult> DeleteProduct(
            HttpContext context,
            Guid id,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var product = await db.Products.FindAsync(id);
                if (product == null)
                {
                    return Results.NotFound(new { error = "Product not found" });
                }

                db.Products.Remove(product);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return InternalServerError("deleting product");
            }
        }

        // ==================== ORDERS HANDLERS (Simplificados) ====================

        private static async Task<IResult> GetOrders(
            HttpContext context,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var totalCount = await db.Orders.CountAsync();
                var orders = await db.Orders
                    .OrderByDescending(o => o.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(o => new AdminOrderListItemDto
                    {
                        Id = o.Id,
                        OrderNumber = o.OrderNumber,
                        Email = o.Email,
                        Total = o.Total,
                        Status = o.Status,
                        CreatedAt = o.CreatedAt
                    })
                    .ToListAsync();

                return Results.Ok(new AdminOrdersResponse
                {
                    Orders = orders,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                return InternalServerError("retrieving orders");
            }
        }

        private static async Task<IResult> GetOrderById(
            HttpContext context,
            Guid id,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var order = await db.Orders
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return Results.NotFound(new { error = "Order not found" });
                }

                // Obtener items por separado
                var orderItems = await db.OrderItems
                    .Where(oi => oi.OrderId == id)
                    .Select(i => new AdminOrderItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        Price = i.Price,
                        Subtotal = i.Subtotal
                    })
                    .ToListAsync();

                var dto = new AdminOrderDetailDto
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    Email = order.Email,
                    Total = order.Total,
                    Subtotal = order.Subtotal,
                    Tax = order.Tax,
                    Shipping = order.Shipping,
                    Status = order.Status,
                    ShippingAddress = order.ShippingAddress,
                    CreatedAt = order.CreatedAt,
                    Items = orderItems
                };

                return Results.Ok(dto);
            }
            catch (Exception ex)
            {
                return InternalServerError("retrieving order");
            }
        }

        private static async Task<IResult> UpdateOrderStatus(
            HttpContext context,
            Guid id,
            [FromBody] UpdateOrderStatusRequest request,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return Results.NotFound(new { error = "Order not found" });
                }

                order.Status = request.Status;
                await db.SaveChangesAsync();

                var dto = new AdminOrderDetailDto
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    Email = order.Email,
                    Total = order.Total,
                    Subtotal = order.Subtotal,
                    Tax = order.Tax,
                    Shipping = order.Shipping,
                    Status = order.Status,
                    ShippingAddress = order.ShippingAddress,
                    CreatedAt = order.CreatedAt,
                    Items = new List<AdminOrderItemDto>()
                };

                return Results.Ok(dto);
            }
            catch (Exception ex)
            {
                return InternalServerError("updating order status");
            }
        }

        // ==================== USERS HANDLERS (Simplificados) ====================

        private static async Task<IResult> GetUsers(
            HttpContext context,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var users = await db.Users
                    .Include(u => u.UserRoles)
                        .ThenInclude(ur => ur.Role)
                    .ToListAsync();

                var userDtos = users.Select(u => new TenantUserListItemDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Roles = u.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Results.Ok(new AdminUsersResponse { Users = userDtos });
            }
            catch (Exception ex)
            {
                return InternalServerError("retrieving users");
            }
        }

        private static async Task<IResult> CreateUser(
            HttpContext context,
            [FromBody] CreateTenantUserRequest request,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver,
            CC.Aplication.Plans.IPlanLimitService planLimitService)  // ? NUEVO
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                // Verificar si el email ya existe
                if (await db.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return Results.Conflict(new { error = "Email already exists" });
                }

                // ? VALIDAR L�MITE DE USUARIOS
                var currentUserCount = await db.Users.CountAsync();
                await planLimitService.ThrowIfExceedsLimitAsync(
                    CC.Infraestructure.Admin.Entities.PlanLimitCodes.MaxUsers,
                    currentUserCount,
                    "Has alcanzado el l�mite de usuarios de tu plan. Actualiza tu plan para agregar m�s usuarios."
                );

                // Hash password
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
                var hash = hasher.HashPassword(null!, request.Password);

                var user = new TenantUser
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email,
                    PasswordHash = hash,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                var dto = new TenantUserDetailDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Roles = new List<string>(),
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                };

                return Results.Created($"/admin/users/{user.Id}", dto);
            }
            catch (CC.Aplication.Plans.PlanLimitExceededException ex)  // ? CATCH ESPEC�FICO
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status402PaymentRequired,
                    title: "Plan Limit Exceeded",
                    detail: ex.Message,
                    extensions: new Dictionary<string, object?>
                    {
                        { "limitCode", ex.LimitCode },
                        { "limitValue", ex.LimitValue },
                        { "currentValue", ex.CurrentValue }
                    }
                );
            }
            catch (Exception ex)
            {
                return InternalServerError("creating user");
            }
        }

        private static async Task<IResult> AssignRole(
            HttpContext context,
            Guid id,
            [FromBody] AssignRoleRequest request,
            TenantDbContextFactory dbFactory,
            ITenantResolver tenantResolver)
        {
            try
            {
                var tenantContext = await tenantResolver.ResolveAsync(context);
                if (tenantContext == null) return TenantNotResolvedError();

                await using var db = dbFactory.Create();

                var user = await db.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return Results.NotFound(new { error = "User not found" });
                }

                var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == request.RoleName);
                if (role == null)
                {
                    return Results.NotFound(new { error = "Role not found" });
                }

                // Verificar si ya tiene el rol
                if (!user.UserRoles.Any(ur => ur.RoleId == role.Id))
                {
                    db.UserRoles.Add(new TenantUserRole
                    {
                        UserId = user.Id,
                        RoleId = role.Id,
                        AssignedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                }

                var dto = new TenantUserDetailDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                };

                return Results.Ok(dto);
            }
            catch (Exception ex)
            {
                return InternalServerError("assigning role");
            }
        }

        // ==================== HELPER METHODS ====================

        private static IResult TenantNotResolvedError()
        {
            return Results.Problem(
                statusCode: 409,
                title: "Tenant Not Resolved",
                detail: "Unable to resolve tenant from request"
            );
        }

        private static IResult InternalServerError(string operation)
        {
            return Results.Problem(
                statusCode: 500,
                title: "Internal Server Error",
                detail: $"An error occurred while {operation}"
            );
        }
    }

    // ==================== DTOs ====================

    // Products
    public record AdminProductsResponse
    {
        public List<ProductListItemDto> Products { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public record ProductListItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public record ProductDetailDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ? RENOMBRADO: AdminCreateProductRequest
    public record AdminCreateProductRequest(
        string Name,
        string Description,
        decimal Price,
        int Stock
    );

    // ? RENOMBRADO: AdminUpdateProductRequest
    public record AdminUpdateProductRequest(
        string? Name,
        string? Description,
        decimal? Price,
        int? Stock,
        bool? IsActive
    );

    // Orders
    public record AdminOrdersResponse
    {
        public List<AdminOrderListItemDto> Orders { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public record AdminOrderListItemDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public record AdminOrderDetailDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Shipping { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<AdminOrderItemDto> Items { get; set; } = new();
    }

    public record AdminOrderItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Subtotal { get; set; }
    }

    public record UpdateOrderStatusRequest(string Status);

    // Users
    public record AdminUsersResponse
    {
        public List<TenantUserListItemDto> Users { get; set; } = new();
    }

    public record TenantUserListItemDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public record TenantUserDetailDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public record CreateTenantUserRequest(
        string Email,
        string Password
    );

    public record AssignRoleRequest(string RoleName);
}

