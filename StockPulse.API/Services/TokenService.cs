using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StockPulse.Domain.Entities;

namespace StockPulse.API.Services;

public sealed class TokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var envSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
        var configuredSecret = _configuration["JwtSettings:Secret"];
        var secret = !string.IsNullOrWhiteSpace(envSecret)
            ? envSecret
            : (!string.IsNullOrWhiteSpace(configuredSecret)
                ? configuredSecret
                : "stockpulse-default-super-secure-jwt-secret-key-2026-min-32-chars");

        var envIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
        var configuredIssuer = _configuration["JwtSettings:Issuer"];
        var issuer = !string.IsNullOrWhiteSpace(envIssuer)
            ? envIssuer
            : (!string.IsNullOrWhiteSpace(configuredIssuer) ? configuredIssuer : "StockPulse");

        var envAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");
        var configuredAudience = _configuration["JwtSettings:Audience"];
        var audience = !string.IsNullOrWhiteSpace(envAudience)
            ? envAudience
            : (!string.IsNullOrWhiteSpace(configuredAudience) ? configuredAudience : "StockPulseClient");

        var lifetimeHoursStr = Environment.GetEnvironmentVariable("TOKEN_LIFETIME_HOURS")
            ?? _configuration["JwtSettings:ExpiryHours"]
            ?? "8";

        if (!int.TryParse(lifetimeHoursStr, out var lifetimeHours) || lifetimeHours <= 0)
        {
            lifetimeHours = 8;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("fullName", user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(lifetimeHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
