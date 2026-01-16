using System.ComponentModel.DataAnnotations;

namespace CC.Aplication.Catalog
{
    #region Product DTOs
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<ProductImageDto> Images { get; set; } = new();
    }

    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int Order { get; set; }
        public bool IsPrimary { get; set; }
    }

    public class CreateProductRequest
    {
        [Required]
        [MinLength(3)]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        public decimal Price { get; set; }

        [Required]
        [Range(0, 999999)]
        public int Stock { get; set; }

        public List<Guid> CategoryIds { get; set; } = new();
    }
    #endregion

    #region Cart DTOs
    public class CartDto
    {
        public Guid Id { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public int TotalItems { get; set; }
    }

    public class CartItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class AddToCartRequest
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
    }

    public class UpdateCartItemRequest
    {
        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
    }
    #endregion

    #region Checkout DTOs
    public class CheckoutQuoteRequest
    {
        [Required]
        [MinLength(3)]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? Phone { get; set; }
    }

    public class CheckoutQuoteResponse
    {
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Shipping { get; set; }
        public decimal Total { get; set; }
        public List<CartItemDto> Items { get; set; } = new();
    }

    public class PlaceOrderRequest
    {
        [Required]
        public string IdempotencyKey { get; set; } = string.Empty;

        [Required]
        [MinLength(3)]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? Phone { get; set; }

        [Required]
        [RegularExpression("^(CARD|CASH|TRANSFER)$")]
        public string PaymentMethod { get; set; } = "CARD";

        /// <summary>
        /// ID de la tienda que cumplirá esta orden (opcional, null = usar stock legacy)
        /// </summary>
        public Guid? StoreId { get; set; }
    }

    public class PlaceOrderResponse
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int? LoyaltyPointsEarned { get; set; }  // Puntos ganados (si loyalty est� habilitado)
    }
    #endregion
}
