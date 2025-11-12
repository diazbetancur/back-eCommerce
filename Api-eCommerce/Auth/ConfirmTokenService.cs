using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Api_eCommerce.Auth
{
    /// <summary>
    /// Servicio para emitir y validar tokens de confirmación con duración corta (15 min)
    /// </summary>
    public interface IConfirmTokenService
    {
        string GenerateConfirmToken(Guid provisioningId, string slug);
        ClaimsPrincipal? ValidateConfirmToken(string token);
    }

    public class ConfirmTokenService : IConfirmTokenService
    {
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;

        public ConfirmTokenService(IConfiguration configuration)
        {
            _secretKey = configuration["JWT:Key"] ?? throw new InvalidOperationException("JWT:Key not configured");
            _issuer = configuration["JWT:Issuer"] ?? "ecommerce-api";
            _audience = configuration["JWT:Audience"] ?? "ecommerce-provisioning";
        }

        public string GenerateConfirmToken(Guid provisioningId, string slug)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, provisioningId.ToString()),
                new Claim("slug", slug),
                new Claim("type", "confirm_provisioning"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15), // Token válido por 15 minutos
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public ClaimsPrincipal? ValidateConfirmToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

                // Verificar que sea un token de confirmación
                var typeClaim = principal.FindFirst("type");
                if (typeClaim?.Value != "confirm_provisioning")
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
