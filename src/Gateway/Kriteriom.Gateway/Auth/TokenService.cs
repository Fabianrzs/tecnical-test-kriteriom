using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Kriteriom.Gateway.Auth;

public class TokenService(IConfiguration config)
{
    private static readonly Dictionary<string, (string PasswordHash, string Role)> Users = new()
    {
        ["admin"]    = ("admin123",    "Admin"),
        ["analyst"]  = ("analyst123",  "Analyst"),
        ["readonly"] = ("readonly123", "ReadOnly")
    };

    public TokenResult? GenerateToken(string username, string password)
    {
        if (!Users.TryGetValue(username, out var user) || user.PasswordHash != password)
            return null;

        return new TokenResult(username, user.Role, GenerateAccessToken(username, user.Role));
    }

    public string GenerateAccessToken(string username, string role)
    {
        var secret   = config["Jwt:Secret"]!;
        var issuer   = config["Jwt:Issuer"]!;
        var audience = config["Jwt:Audience"]!;
        var expiry   = config.GetValue<int>("Jwt:ExpiryMinutes", 60);

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim("sub", username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:   issuer,
            audience: audience,
            claims:   claims,
            expires:  DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public record TokenResult(string Username, string Role, string AccessToken);

    public TokenValidationParameters GetValidationParameters()
    {
        var secret = config["Jwt:Secret"]!;
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer   = true,
            ValidIssuer      = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience    = config["Jwt:Audience"],
            ClockSkew        = TimeSpan.Zero
        };
    }
}
