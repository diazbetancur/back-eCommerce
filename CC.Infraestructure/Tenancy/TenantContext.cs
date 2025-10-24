namespace CC.Infraestructure.Tenancy
{
 public class TenantContext
 {
 public Guid TenantId { get; set; }
 public string Slug { get; set; }
 public Guid? PlanId { get; set; }
 public string ConnectionString { get; set; }
 }
}