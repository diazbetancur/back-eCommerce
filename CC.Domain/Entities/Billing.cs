namespace CC.Domain.Entities
{
    public class Billing : EntityBase<Guid>
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; }
        public DateTime BuyDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PayType { get; set; }
        public decimal DiscountAmount { get; set; }
        public string BillingStatus { get; set; }
        public decimal IVA { get; set; }
        public List<ProductsBilling> ProductsBillings { get; set; } = new();
    }
}