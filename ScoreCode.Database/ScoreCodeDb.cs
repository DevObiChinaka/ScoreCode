using Microsoft.EntityFrameworkCore;
using ScoreCode.Database.Entities;

namespace ScoreCode.Database;

public class ScoreCodeDb : DbContext
{
    public ScoreCodeDb(DbContextOptions<ScoreCodeDb> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamStudent> ExamStudents => Set<ExamStudent>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        // Composite PK for join table
        m.Entity<ExamStudent>()
            .HasKey(es => new { es.ExamId, es.StudentId });

        // IdNumber must be unique across all users
        m.Entity<User>()
            .HasIndex(u => u.IdNumber)
            .IsUnique();

        // Exam → Lecturer (restrict delete so deleting a user
        // doesn't cascade-delete all their exams silently)
        m.Entity<Exam>()
            .HasOne(e => e.Lecturer)
            .WithMany(u => u.CreatedExams)
            .HasForeignKey(e => e.LecturerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Submission is unique per student per question
        m.Entity<Submission>()
            .HasIndex(s => new { s.StudentId, s.QuestionId })
            .IsUnique();

        // Grade is one-to-one with Submission
        m.Entity<Grade>()
            .HasOne(g => g.Submission)
            .WithOne(s => s.Grade)
            .HasForeignKey<Grade>(g => g.SubmissionId);

        // RefreshToken → User, unique token string
        m.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        m.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();
    }
}