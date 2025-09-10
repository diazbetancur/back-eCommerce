using CC.Domain.Entities;

namespace CC.Domain.Dto
{
    public class BillingDto
    {
        public Guid? Id { get; set; }
        public Guid? UserId { get; set; }
        public DateTime BuyDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PayType { get; set; }
        public decimal DiscountAmount { get; set; }
        public string BillingStatus { get; set; }
        public decimal IVA { get; set; }
        public List<ProductsBillingDto> ProductsBillings { get; set; } = new();
    }
}