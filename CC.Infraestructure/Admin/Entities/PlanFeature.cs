using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
 [Table("PlanFeatures", Schema = "admin")]
 public class PlanFeature
 {
 public Guid PlanId { get; set; }
 public Plan Plan { get; set; }
 public Guid FeatureId { get; set; }
 public Feature Feature { get; set; }
 public bool Enabled { get; set; }
 public int? LimitValue { get; set; }
 }
}