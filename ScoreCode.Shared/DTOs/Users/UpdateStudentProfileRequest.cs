namespace ScoreCode.Shared.DTOs.Users;

public class UpdateStudentProfileRequest
{
    /// <summary>
    /// Lecturers only. Students are deliberately anonymous — they are identified
    /// by matric number alone, so a name set on a student account is ignored.
    /// </summary>
    public string? Name { get; set; }

    public string? MatricNumber { get; set; }

    /// <summary>Recovery address. Blank clears it, sending the user back to admin resets.</summary>
    public string? Email { get; set; }
    public string? ProgrammeOfStudy { get; set; }
    public string? Level { get; set; }
    public string? Department { get; set; }
}
