using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CC.Domain.Entities
{
    public class ProductImage : EntityBase<Guid>
    {
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; }
        public string ImageUrl { get; set; }
        public string ImageName { get; set; }
        public bool IsPrimary { get; set; } = false;
    }
}