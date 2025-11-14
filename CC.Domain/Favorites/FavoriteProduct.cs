using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Domain.Favorites
{
    /// <summary>
    /// Producto favorito de un usuario (wishlist)
    /// </summary>
    [Table("FavoriteProducts")]
    public class FavoriteProduct
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties (opcional, pero útil para EF Core)
        // public UserAccount? User { get; set; }
        // public Product? Product { get; set; }
    }
}
