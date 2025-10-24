using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
 [Table("TenantUsageDaily", Schema = "admin")]
 public class TenantUsageDaily
 {
 public Guid TenantId { get; set; }
 public DateOnly Date { get; set; }
 public int ReqCount { get; set; }
 public int PushCount { get; set; }
 public int ErrorCount { get; set; }
 public int StorageMbEst { get; set; }
 }
}