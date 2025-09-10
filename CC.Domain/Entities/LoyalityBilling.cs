namespace CC.Domain.Entities
{
    public class LoyalityBilling : EntityBase<Guid>
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; }
        public DateOnly ExpirationPoints { get; set; }
        public double Points { get; set; }
        public string TransactionType { get; set; } // e.g., "Earned", "Redeemed"
        public Guid BillingId { get; set; }
        public virtual Billing Billing { get; set; }
    }
}