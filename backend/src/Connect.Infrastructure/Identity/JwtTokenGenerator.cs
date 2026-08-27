using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Connect.Infrastructure.Identity;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var secret = _configuration["JwtSettings:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            secret = "SuperSecretKeyForConnectAppJwtTokenGeneration2026!";
        }

        var issuer = _configuration["JwtSettings:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            issuer = "ConnectApi";
        }

        var audience = _configuration["JwtSettings:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            audience = "ConnectClient";
        }

        var expiryMinutesStr = _configuration["JwtSettings:ExpiryMinutes"];
        if (string.IsNullOrWhiteSpace(expiryMinutesStr))
        {
            expiryMinutesStr = "43200";
        }
        var expiryMinutes = double.TryParse(expiryMinutesStr, out var exp) ? exp : 43200;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserId),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("UserId", user.UserId),
            new Claim("SubscriptionTier", user.SubscriptionTier.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
