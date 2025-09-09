using System;

namespace CC.Domain.Dto
{
    public class ProductCategoryDto
    {
        public Guid ProductId { get; set; }
        public Guid CategoryId { get; set; }
        public bool IsPrimary { get; set; }
        public CategoryDto Category { get; set; }
    }
}
