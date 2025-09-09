using System;
using System.Collections.Generic;
using CC.Domain.Enums;

namespace CC.Domain.Dto
{
    public class ProductPropertyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public PropertyType Type { get; set; }
        public bool IsFilterable { get; set; }
        public bool IsSearchable { get; set; }
        public int FilterOrder { get; set; }
        public List<string> PredefinedValues { get; set; }
        public string Unit { get; set; }
    }
}
