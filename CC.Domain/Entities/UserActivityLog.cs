namespace CC.Domain.Entities
{
    public class UserActivityLog : EntityBase<Guid>
    {
        public Guid UserId { get; set; }
        public virtual User User { get; set; }
        public string Action { get; set; }
        public string IpAddress { get; set; }
    }
}