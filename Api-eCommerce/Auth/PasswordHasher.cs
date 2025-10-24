using Microsoft.AspNetCore.Identity;

namespace Api_eCommerce.Auth
{
 public interface IPasswordHasher
 {
 string Hash(string password);
 bool Verify(string hash, string password);
 }

 public class AspNetPasswordHasher : IPasswordHasher
 {
 private readonly PasswordHasher<object> _hasher = new();
 public string Hash(string password) => _hasher.HashPassword(null!, password);
 public bool Verify(string hash, string password)
 {
 var result = _hasher.VerifyHashedPassword(null!, hash, password);
 return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
 }
 }
}