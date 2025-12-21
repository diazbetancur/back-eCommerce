namespace CC.Infraestructure.Tenant.Entities
{
    /// <summary>
    /// Producto del catálogo del tenant
    /// </summary>
    public class Product
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty; // URL amigable: "camisa-azul-premium"
        public string? Sku { get; set; } // Código único del producto
        public string? Description { get; set; }
        public string? ShortDescription { get; set; } // Para listados
        public decimal Price { get; set; }
        public decimal? CompareAtPrice { get; set; } // Precio anterior (tachado)
        public int Stock { get; set; }
        public bool TrackInventory { get; set; } = true; // Si false, no validar stock
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; } = false; // Para mostrar en home
        public string? Tags { get; set; } // "verano,oferta,nuevo" - para búsqueda
        public string? Brand { get; set; }

        // SEO
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        // Imágenes (la principal se maneja en ProductImage con IsPrimary)
        public string? MainImageUrl { get; set; } // Cache de la imagen principal

        // Auditoría
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navegación
        public virtual ICollection<ProductCategory>? Categories { get; set; }
        public virtual ICollection<ProductImage>? Images { get; set; }
    }

    /// <summary>
    /// Categoría de productos con soporte para jerarquía
    /// </summary>
    public class Category
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty; // URL amigable: "ropa-hombre"
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }

        // Jerarquía
        public Guid? ParentId { get; set; } // null = categoría raíz
        public virtual Category? Parent { get; set; }
        public virtual ICollection<Category>? Children { get; set; }

        // Display
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public bool ShowInMenu { get; set; } = true;

        // SEO
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        // Navegación
        public virtual ICollection<ProductCategory>? Products { get; set; }
    }

    /// <summary>
    /// Banner promocional con soporte para web y móvil (PWA)
    /// </summary>
    public class Banner
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }

        // Imágenes responsive
        public string ImageUrlDesktop { get; set; } = string.Empty; // Recomendado: 1920x600
        public string? ImageUrlMobile { get; set; } // Recomendado: 768x400 (para PWA)

        // Acción
        public string? TargetUrl { get; set; } // A dónde lleva el click
        public string? ButtonText { get; set; } // "Ver ofertas", "Comprar ahora"

        // Posición/Tipo
        public BannerPosition Position { get; set; } = BannerPosition.Hero;

        // Programación
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Display
        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        // Auditoría
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum BannerPosition
    {
        Hero = 0,      // Banner principal grande
        Secondary = 1, // Banners secundarios más pequeños
        Sidebar = 2,   // Lateral
        Popup = 3,     // Modal/popup
        Footer = 4     // Pie de página
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