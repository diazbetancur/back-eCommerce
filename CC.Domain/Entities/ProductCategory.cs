using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CC.Domain.Entities
{
    public class ProductCategory : EntityBase<Guid>
    {
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; }
        public Guid CategoryId { get; set; }
        public virtual Category Category { get; set; }
        public bool IsPrimary { get; set; } = false;
    }
}