using Microsoft.AspNetCore.Identity;

namespace CC.Domain.Entities
{
    public class Role : IdentityRole<Guid>
    {
        public bool IsActive { get; set; }
    }
}