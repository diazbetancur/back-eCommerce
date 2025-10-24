using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CC.Infraestructure.Admin.Entities
{
 [Table("Features", Schema = "admin")]
 public class Feature
 {
 [Key]
 public Guid Id { get; set; }
 [Required, MaxLength(50)]
 public string Code { get; set; }
 [Required, MaxLength(100)]
 public string Name { get; set; }
 public bool IsBoolean { get; set; }
 }
}