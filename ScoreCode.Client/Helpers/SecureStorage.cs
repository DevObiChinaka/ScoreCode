using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScoreCode.Shared.DTOs.Auth;

namespace ScoreCode.Client.Helpers;

/// <summary>
/// Stores session tokens locally using Windows DPAPI encryption.
/// Data is only decryptable by the same Windows user account on the same machine.
/// </summary>
public static class SecureStorage
{
    private static readonly string StoragePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScoreCode", "session.dat");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void SaveSession(LoginResponse loginResp)
    {
        var data = new StoredSession
        {
            AccessToken = loginResp.AccessToken,
            AccessTokenExpiry = loginResp.AccessTokenExpiry,
            RefreshToken = loginResp.RefreshToken,
            UserId = loginResp.User.Id,
            Name = loginResp.User.Name,
            IdNumber = loginResp.User.IdNumber,
            Role = loginResp.User.Role
        };

        var json = JsonSerializer.Serialize(data, JsonOpts);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        File.WriteAllBytes(StoragePath, encrypted);
    }

    public static StoredSession? LoadSession()
    {
        if (!File.Exists(StoragePath)) return null;

        try
        {
            var encrypted = File.ReadAllBytes(StoragePath);
            var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            return JsonSerializer.Deserialize<StoredSession>(json, JsonOpts);
        }
        catch
        {
            // Corrupted or from a different user — treat as no session
            ClearSession();
            return null;
        }
    }

    public static void ClearSession()
    {
        if (File.Exists(StoragePath))
            File.Delete(StoragePath);
    }

    public static bool HasValidSession()
    {
        var session = LoadSession();
        return session is not null && session.AccessTokenExpiry > DateTime.UtcNow.AddMinutes(1);
    }
}

public class StoredSession
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiry { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
