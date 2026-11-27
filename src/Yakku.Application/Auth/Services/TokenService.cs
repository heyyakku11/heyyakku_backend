using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Yakku.Application.Auth.Interfaces;

namespace Yakku.Application.Auth.Services
{
    public class TokenService : ITokenService
    {
        private readonly byte[] _signingKey;
        private readonly TimeSpan _accessTokenLifetime;

        public TokenService(string secret, TimeSpan? accessTokenLifetime = null)
        {
            if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            {
                throw new InvalidOperationException(
                    "JWT_SECRET must be at least 32 characters. Add it to your .env file.");
            }

            _signingKey = Encoding.UTF8.GetBytes(secret);
            _accessTokenLifetime = accessTokenLifetime ?? JwtOptions.AccessTokenLifetime;
        }

        public string CreateAccessToken(Guid userId, Guid sessionId)
        {
            return CreateAccessToken(userId, sessionId, _accessTokenLifetime);
        }

        public string CreateAccessToken(Guid userId, Guid sessionId, TimeSpan lifetime)
        {
            var now = DateTime.UtcNow;
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(AuthClaimTypes.SessionId, sessionId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(_signingKey),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                notBefore: now,
                expires: now.Add(lifetime),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string CreateAdminAccessToken(Guid adminId, Guid sessionId, string email)
        {
            var now = DateTime.UtcNow;
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, adminId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(AuthClaimTypes.Actor, AuthClaimTypes.AdminActor),
                new Claim(AuthClaimTypes.SessionId, sessionId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(_signingKey),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims: claims,
                notBefore: now,
                expires: now.Add(_accessTokenLifetime),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
