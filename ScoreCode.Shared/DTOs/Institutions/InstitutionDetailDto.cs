using ScoreCode.Shared.DTOs.Users;

namespace ScoreCode.Shared.DTOs.Institutions;

/// <summary>Platform-admin drill-down for a single institution.</summary>
public class InstitutionDetailDto
{
    public InstitutionDto Institution { get; set; } = new();
    public UserDto? Admin { get; set; }
    public int Lecturers { get; set; }
    public int Students { get; set; }
    public int Exams { get; set; }
    public List<UserDto> RecentLecturers { get; set; } = [];
    public List<UserDto> RecentStudents { get; set; } = [];
}
