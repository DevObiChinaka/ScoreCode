namespace ScoreCode.Shared.DTOs.Auth;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiry { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public UserInfo User { get; set; } = null!;

    public class UserInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IdNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        /// <summary>True when an admin issued a temporary password that must be replaced.</summary>
        public bool MustChangePassword { get; set; }
    }
}
