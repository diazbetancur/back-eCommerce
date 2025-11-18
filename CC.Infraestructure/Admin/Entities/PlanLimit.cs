using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
    /// <summary>
    /// Límites configurables por plan
    /// Define restricciones numéricas como máximo de imágenes, productos, usuarios, etc.
    /// Valor -1 = ilimitado
    /// </summary>
    [Table("PlanLimits", Schema = "admin")]
    public class PlanLimit
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PlanId { get; set; }

        /// <summary>
        /// Código del límite (ej: "max_product_images", "max_users", "max_storage_mb")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string LimitCode { get; set; } = string.Empty;

        /// <summary>
        /// Valor del límite (-1 = ilimitado, 0 = bloqueado, N = límite específico)
        /// </summary>
        [Required]
        public int LimitValue { get; set; }

        /// <summary>
        /// Descripción del límite (opcional)
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Plan Plan { get; set; } = null!;
    }

    /// <summary>
    /// Códigos estándar de límites del sistema
    /// </summary>
    public static class PlanLimitCodes
    {
        // Productos
        public const string MaxProductImages = "max_product_images";
        public const string MaxProductVideos = "max_product_videos";
        public const string MaxProducts = "max_products";
        public const string MaxCategories = "max_categories";

        // Usuarios
        public const string MaxUsers = "max_users";
        public const string MaxAdminUsers = "max_admin_users";

        // Órdenes
        public const string MaxOrdersPerMonth = "max_orders_per_month";
        public const string MaxOrdersPerDay = "max_orders_per_day";

        // Almacenamiento
        public const string MaxStorageMB = "max_storage_mb";
        public const string MaxFileUploadMB = "max_file_upload_mb";

        // Loyalty
        public const string MaxLoyaltyPointsPerOrder = "max_loyalty_points_per_order";
        public const string MaxLoyaltyPointsPerUser = "max_loyalty_points_per_user";

        // API
        public const string MaxApiCallsPerDay = "max_api_calls_per_day";
        public const string MaxApiCallsPerMinute = "max_api_calls_per_minute";

        // Email/Notificaciones
        public const string MaxEmailsPerDay = "max_emails_per_day";
        public const string MaxPushNotificationsPerDay = "max_push_notifications_per_day";
    }
}
