namespace CC.Infraestructure.Tenant.Entities
{
    // Catálogo
    public class ProductCategory
    {
        public Guid ProductId { get; set; }
        public Guid CategoryId { get; set; }
    }

    public class ProductImage
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsPrimary { get; set; }
    }

    // Carrito de Compras
    public class Cart
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string? SessionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navegación
        public virtual ICollection<CartItem>? Items { get; set; }
    }

    public class CartItem
    {
        public Guid Id { get; set; }
        public Guid CartId { get; set; }
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }

    // Pedidos
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class OrderStatus
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty; // PENDING, PROCESSING, SHIPPED, DELIVERED, CANCELLED
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
