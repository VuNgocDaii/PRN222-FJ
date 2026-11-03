using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuanLyCuDan.Models;

namespace QuanLyCuDan.Auth;
// Optional Auth:SigningKey is a base64 secret supplied outside source control.
// Local fallback lasts for this process; refresh supplies a new JWT after restart.
public sealed class TokenIssuer
{
    private readonly SymmetricSecurityKey key;
    public TokenIssuer(IConfiguration configuration)
    {
        var configured = configuration["Auth:SigningKey"];
        var bytes = string.IsNullOrWhiteSpace(configured) ? RandomNumberGenerator.GetBytes(32) : Convert.FromBase64String(configured);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Auth:SigningKey must contain at least 32 random bytes.");
        key = new SymmetricSecurityKey(bytes);
    }

    public string Issue(AspNetUser user, UserSession session)
    {
        var now = DateTime.UtcNow;
        var expires = new[]
        {
            now.AddMinutes(10),
            session.AbsoluteExpiresAt
        }.Min();
        var token = new JwtSecurityToken("QuanLyCuDan", "QuanLyCuDan.Api", new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id), new Claim("sid", session.Id.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName), new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) }, now, expires, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal Validate(string token) => new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = "QuanLyCuDan", ValidateAudience = true, ValidAudience = "QuanLyCuDan.Api", ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ValidateIssuerSigningKey = true, IssuerSigningKey = key, ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, ClockSkew = TimeSpan.Zero, NameClaimType = ClaimTypes.Name }, out _);
}
