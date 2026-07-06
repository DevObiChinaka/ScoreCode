using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ScoreCode.Database;
using ScoreCode.Database.Entities;

namespace ScoreCode.Api.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly ScoreCodeDb _db;

    public TokenService(IConfiguration config, ScoreCodeDb db)
    {
        _config = config;
        _db = db;
    }

    public string CreateAccessToken(User user)
    {
        var key = _config["Jwt:Key"]!;
        var expiryMinutes = _config.GetValue<int>("Jwt:AccessTokenExpiryMinutes", 15);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("idNumber", user.IdNumber),
            new(ClaimTypes.Role, user.Role),
            new(ClaimTypes.Name, user.Name)
        };

        // Tenant claim — absent for the platform admin (no institution).
        if (user.InstitutionId.HasValue)
            claims.Add(new Claim("institutionId", user.InstitutionId.Value.ToString()));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId)
    {
        var expiryDays = _config.GetValue<int>("Jwt:RefreshTokenExpiryDays", 7);

        // Generate a cryptographically random token
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var tokenString = Convert.ToBase64String(tokenBytes);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
    {
        return await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt =>
                rt.Token == token &&
                !rt.IsRevoked &&
                rt.ExpiresAt > DateTime.UtcNow);
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);
        if (rt is not null)
        {
            rt.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }
}
