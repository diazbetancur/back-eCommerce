using System.Security.Claims;

namespace Api_eCommerce.Auth
{
 public interface ITokenService
 {
 string CreateToken(IEnumerable<Claim> claims, DateTime expiresAt);
 }
}