namespace CC.Domain.Entities
{
    public class Product : EntityBase<Guid>
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public decimal Discount { get; set; } = 0;

        // Propiedades dinámicas
        public Dictionary<string, object> DynamicAttributes { get; set; } = new();

        // Múltiples categorías
        public List<ProductCategory> ProductCategories { get; set; } = new();

        public string SearchableText { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}