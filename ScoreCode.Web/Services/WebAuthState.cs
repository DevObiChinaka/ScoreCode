namespace ScoreCode.Web.Services;

/// <summary>
/// Per-circuit (in-memory) auth state. Persists across in-app navigation but is
/// lost on a hard browser refresh (which starts a new circuit) — the user is
/// then sent back to /login. A durable store (ProtectedSessionStorage/cookies)
/// can replace this later.
/// </summary>
public class WebAuthState
{
    public string? Token { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;

    /// <summary>True while the signed-in user is still on an admin-issued password.</summary>
    public bool MustChangePassword { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
    public bool IsPlatformAdmin => Role is "PlatformAdmin" or "Admin";
    public bool IsInstitutionAdmin => Role == "InstitutionAdmin";

    public void Set(string token, string role, string name, bool mustChangePassword = false)
    {
        Token = token;
        Role = role;
        Name = name;
        MustChangePassword = mustChangePassword;
    }

    /// <summary>Called once the user has set their own password.</summary>
    public void PasswordChanged() => MustChangePassword = false;

    public void Clear()
    {
        Token = null;
        Role = string.Empty;
        Name = string.Empty;
        MustChangePassword = false;
    }
}
