using ScoreCode.Database.Entities;

namespace ScoreCode.Api.Services;

public interface ITokenService
{
    string CreateAccessToken(User user);
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token);
    Task RevokeRefreshTokenAsync(string token);
}
