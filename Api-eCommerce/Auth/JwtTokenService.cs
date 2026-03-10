using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api_eCommerce.Auth
{
  public class JwtTokenService : ITokenService
  {
    private readonly string _key;
    private readonly string? _issuer;
    private readonly string? _audience;
    public JwtTokenService(IConfiguration cfg)
    {
      _key = cfg["JWT:Key"] ?? cfg["Jwt:SigningKey"] ?? cfg["jwtKey"] ?? throw new InvalidOperationException("JWT signing key not configured");
      _issuer = cfg["JWT:Issuer"] ?? cfg["Jwt:Issuer"];
      _audience = cfg["JWT:Audience"] ?? cfg["Jwt:Audience"];
    }

    public string CreateToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
      var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)), SecurityAlgorithms.HmacSha256);
      var token = new JwtSecurityToken(
      issuer: _issuer,
      audience: _audience,
      claims: claims,
      notBefore: DateTime.UtcNow,
      expires: expiresAt,
      signingCredentials: creds);
      return new JwtSecurityTokenHandler().WriteToken(token);
    }
  }
}