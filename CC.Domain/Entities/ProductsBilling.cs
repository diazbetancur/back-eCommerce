namespace CC.Domain.Entities
{
    public class ProductsBilling : EntityBase<Guid>
    {
        public Guid ProductId { get; set; }
        public virtual Product Product { get; set; }
        public Guid BillingId { get; set; }
        public virtual Billing Billing { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
        public decimal Discount { get; set; }
        public decimal IVA { get; set; }
    }
}