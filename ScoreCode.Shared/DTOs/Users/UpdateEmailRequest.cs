namespace ScoreCode.Shared.DTOs.Users;

/// <summary>A user changing their own recovery email. Blank clears it.</summary>
public class UpdateEmailRequest
{
    public string? Email { get; set; }
}
