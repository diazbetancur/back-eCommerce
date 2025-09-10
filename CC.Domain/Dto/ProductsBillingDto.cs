namespace CC.Domain.Dto
{
    public class ProductsBillingDto
    {
        public Guid? Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid BillingId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
        public decimal Discount { get; set; }
        public decimal IVA { get; set; }
    }
}