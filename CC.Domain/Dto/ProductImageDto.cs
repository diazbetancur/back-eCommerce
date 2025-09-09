using System;
using System.Collections.Generic;
using CC.Domain.Enums;

namespace CC.Domain.Dto
{
    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ImageUrl { get; set; }
        public string ImageName { get; set; }
        public bool IsPrimary { get; set; }
    }
}