namespace ScoreCode.Database.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;   // matric no | staff ID | admin ID
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;        // Admin | Lecturer | Student
    public string Department { get; set; } = string.Empty;
    public string ProgrammeOfStudy { get; set; } = string.Empty; // students only
    public string Level { get; set; } = string.Empty;       // students only (e.g. "300")
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional recovery address, used only to send a password-reset code.
    /// Never exposed to lecturers: a student's email would identify them, and
    /// students are anonymous to their markers. Accounts without one fall back
    /// to an administrator-issued reset.
    /// </summary>
    public string? Email { get; set; }

    // Security
    /// <summary>Set when an admin issues a temporary password; cleared on change.</summary>
    public bool MustChangePassword { get; set; }
    /// <summary>Consecutive failed sign-ins; reset on success.</summary>
    public int FailedLoginAttempts { get; set; }
    /// <summary>Sign-in is refused until this time after too many failures.</summary>
    public DateTime? LockoutUntil { get; set; }

    // Self-service reset. One code per user at a time; stored hashed so a
    // database read cannot be turned into an account takeover.
    public string? ResetOtpHash { get; set; }
    public DateTime? ResetOtpExpiresAt { get; set; }
    /// <summary>Wrong codes entered for the current OTP; too many voids it.</summary>
    public int ResetOtpAttempts { get; set; }

    // Tenant — null only for the platform admin (no institution)
    public Guid? InstitutionId { get; set; }
    public Institution? Institution { get; set; }

    // Navigation
    public ICollection<Exam> CreatedExams { get; set; } = [];
    public ICollection<ExamStudent> ExamStudents { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}