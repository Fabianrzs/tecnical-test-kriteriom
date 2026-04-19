using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Kriteriom.Gateway.Auth;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly Dictionary<string, (string Password, string Role)> _users;

    public TokenService(IConfiguration config)
    {
        _config = config;

        // Load users from configuration (Vault / env vars in production)
        // Default fallback values are for local development only
        _users = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"]    = (config["Auth:Admin:Password"]    ?? "admin123",    config["Auth:Admin:Role"]    ?? "Admin"),
            ["analyst"]  = (config["Auth:Analyst:Password"]  ?? "analyst123",  config["Auth:Analyst:Role"]  ?? "Analyst"),
            ["readonly"] = (config["Auth:ReadOnly:Password"] ?? "readonly123", config["Auth:ReadOnly:Role"] ?? "ReadOnly"),
        };
    }

    public TokenResult? GenerateToken(string username, string password)
    {
        if (!_users.TryGetValue(username, out var user) || user.Password != password)
            return null;

        return new TokenResult(username, user.Role, GenerateAccessToken(username, user.Role));
    }

    public string GenerateAccessToken(string username, string role)
    {
        var secret   = _config["Jwt:Secret"]!;
        var issuer   = _config["Jwt:Issuer"]!;
        var audience = _config["Jwt:Audience"]!;
        var expiry   = _config.GetValue<int>("Jwt:ExpiryMinutes", 60);

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
            issuer:            issuer,
            audience:          audience,
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(expiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public record TokenResult(string Username, string Role, string AccessToken);

    public TokenValidationParameters GetValidationParameters()
    {
        var secret = _config["Jwt:Secret"]!;
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer           = true,
            ValidIssuer              = _config["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = _config["Jwt:Audience"],
            ClockSkew                = TimeSpan.Zero
        };
    }
}
