using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Tenant.Entities
{
    /// <summary>
    /// Módulo del sistema (Sales, Inventory, Reports, etc.)
    /// Define las diferentes áreas funcionales del tenant
    /// </summary>
    [Table("Modules")]
    public class Module
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;  // "sales", "inventory", "reports"

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;  // "Gestión de Ventas"

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(50)]
        public string? IconName { get; set; }  // "shopping-cart", "box", "chart-bar"

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<RoleModulePermission> RolePermissions { get; set; } = new List<RoleModulePermission>();
    }
}
