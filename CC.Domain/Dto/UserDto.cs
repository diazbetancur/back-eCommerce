namespace CC.Domain.Dto
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string? Password { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}