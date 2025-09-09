using CC.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CC.Domain.Dto
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; }
        public JsonDocument? DynamicAttributes { get; set; }
        public List<ProductCategoryDto>? ProductCategories { get; set; }
        public List<ProductImageDto>? ProductImages { get; set; }
        public string SearchableText { get; set; }
        public bool IsDeleted { get; set; }
    }
}