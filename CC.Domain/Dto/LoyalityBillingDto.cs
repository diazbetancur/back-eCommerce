namespace CC.Domain.Dto
{
    public class LoyalityBillingDto
    {
        public Guid? Id { get; set; }
        public Guid UserId { get; set; }
        public DateOnly ExpirationPoints { get; set; }
        public double Points { get; set; }
        public string TransactionType { get; set; } // e.g., "Earned", "Redeemed"
        public Guid BillingId { get; set; }
    }
}