using System;

namespace CC.Domain.Dto
{
    public class CategoryDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public string? ImageName { get; set; }
    }
}