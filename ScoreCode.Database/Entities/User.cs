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

    // Tenant — null only for the platform admin (no institution)
    public Guid? InstitutionId { get; set; }
    public Institution? Institution { get; set; }

    // Navigation
    public ICollection<Exam> CreatedExams { get; set; } = [];
    public ICollection<ExamStudent> ExamStudents { get; set; } = [];
    public ICollection<Session> Sessions { get; set; } = [];
}