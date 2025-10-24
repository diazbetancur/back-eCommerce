namespace CC.Infraestructure.Tenant.Entities
{
 public class Product { public Guid Id { get; set; } public string Name { get; set; } }
 public class Category { public Guid Id { get; set; } public string Name { get; set; } }
 public class Order { public Guid Id { get; set; } public DateTime CreatedAt { get; set; } }
}