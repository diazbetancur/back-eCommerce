using System.Security.Claims;

namespace CC.Domain.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static Guid? GetUserId(this ClaimsPrincipal user)
        {
            var userIdStr = user.FindFirst("UserId")?.Value;
            return Guid.TryParse(userIdStr, out var userId) ? userId : null;
        }

        public static List<string> GetRoles(this ClaimsPrincipal user)
        {
            var rolesStr = user.FindFirst("Role")?.Value;
            return string.IsNullOrEmpty(rolesStr)
                ? new List<string>()
                : rolesStr.Split(',').ToList();
        }
    }
}