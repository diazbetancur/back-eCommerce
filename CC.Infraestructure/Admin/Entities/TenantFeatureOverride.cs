using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
 [Table("TenantFeatureOverrides", Schema = "admin")]
 public class TenantFeatureOverride
 {
 public Guid TenantId { get; set; }
 public Tenant Tenant { get; set; }
 public Guid FeatureId { get; set; }
 public Feature Feature { get; set; }
 public bool? Enabled { get; set; }
 public int? LimitValue { get; set; }
 }
}