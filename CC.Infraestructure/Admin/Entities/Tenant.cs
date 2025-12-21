using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
    public enum TenantStatus
    {
        Pending = 0,
        Seeding = 1,
        Ready = 2,
        Suspended = 3,
        Failed = 4,
        Disabled = 5
    }

    [Table("Tenants", Schema = "admin")]
    public class Tenant
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; }

        [Required]
        [MaxLength(100)]
        public string DbName { get; set; }

        public TenantStatus Status { get; set; }

        // ? CAMBIAR: Nullable porque se asigna despu�s de crear la DB
        public string? EncryptedConnection { get; set; }

        public Guid? PlanId { get; set; }
        public Plan? Plan { get; set; }
        public string? FeatureFlagsJson { get; set; }
        public string? AllowedOrigins { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? LastError { get; set; }
    }
}