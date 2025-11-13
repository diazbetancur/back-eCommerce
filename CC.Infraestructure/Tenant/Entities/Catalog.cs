namespace CC.Infraestructure.Tenant.Entities
{
    public class Product 
    { 
        public Guid Id { get; set; } 
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
    
    public class Category 
    { 
        public Guid Id { get; set; } 
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
    
    public class Order 
    { 
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string? SessionId { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Shipping { get; set; }
        public string Status { get; set; } = "PENDING";
        public string ShippingAddress { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string PaymentMethod { get; set; } = "CARD";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}