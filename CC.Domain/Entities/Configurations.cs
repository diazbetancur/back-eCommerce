namespace CC.Domain.Entities
{
    public class Configurations : EntityBase<Guid>
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
    }
}