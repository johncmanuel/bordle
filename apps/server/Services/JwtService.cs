using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Bordle.Server.Services
{
    public class JwtService(IConfiguration configuration)
    {
        private readonly string _secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        private readonly string _issuer = configuration["Jwt:Issuer"] ?? "bordle";
        private readonly string _audience = configuration["Jwt:Audience"] ?? "bordle";

        // generate a JWT token with userId and guildId as claims, used for issuing Discord tokens
        public string GenerateToken(long userId, long guildId)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("userId", userId.ToString()),
                new Claim("guildId", guildId.ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _issuer,
                audience: _audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
