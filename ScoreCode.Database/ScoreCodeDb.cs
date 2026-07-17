using Microsoft.EntityFrameworkCore;
using ScoreCode.Database.Entities;

namespace ScoreCode.Database;

public class ScoreCodeDb : DbContext
{
    private readonly ITenantProvider? _tenant;

    // Design-time / startup (no tenant scoping).
    public ScoreCodeDb(DbContextOptions<ScoreCodeDb> options) : base(options) { }

    // Runtime (DI injects the request's tenant provider).
    public ScoreCodeDb(DbContextOptions<ScoreCodeDb> options, ITenantProvider tenant) : base(options)
        => _tenant = tenant;

    /// <summary>The tenant to scope queries to, or null to see everything.</summary>
    private Guid? CurrentTenantId => _tenant?.CurrentInstitutionId;

    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamStudent> ExamStudents => Set<ExamStudent>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();

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

        // ── Tenancy: Institution relationships ─────────────────────────────
        // All tenant FKs are Restrict — an institution is never cascade-deleted
        // out from under its users/exams/sessions/submissions.
        m.Entity<User>()
            .HasOne(u => u.Institution)
            .WithMany(i => i.Users)
            .HasForeignKey(u => u.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        m.Entity<Exam>()
            .HasOne(e => e.Institution)
            .WithMany()
            .HasForeignKey(e => e.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        m.Entity<Session>()
            .HasOne(s => s.Institution)
            .WithMany()
            .HasForeignKey(s => s.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        m.Entity<Submission>()
            .HasOne(s => s.Institution)
            .WithMany()
            .HasForeignKey(s => s.InstitutionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Tenant isolation: global query filters ─────────────────────────
        // When CurrentTenantId is null (platform admin, background jobs, or an
        // anonymous request such as login) nothing is filtered; otherwise every
        // read of a tenant-owned entity is confined to that institution.
        m.Entity<User>().HasQueryFilter(u => CurrentTenantId == null || u.InstitutionId == CurrentTenantId);
        m.Entity<Exam>().HasQueryFilter(e => CurrentTenantId == null || e.InstitutionId == CurrentTenantId);
        m.Entity<Session>().HasQueryFilter(s => CurrentTenantId == null || s.InstitutionId == CurrentTenantId);
        m.Entity<Submission>().HasQueryFilter(s => CurrentTenantId == null || s.InstitutionId == CurrentTenantId);

        // Children scope through their tenant-owning parent, so filters stay
        // consistent (removes EF's required-navigation warnings) and no query
        // can pull a child whose parent is out of tenant.
        m.Entity<Question>().HasQueryFilter(q => CurrentTenantId == null || q.Exam.InstitutionId == CurrentTenantId);
        m.Entity<ExamStudent>().HasQueryFilter(es => CurrentTenantId == null || es.Exam.InstitutionId == CurrentTenantId);
        m.Entity<Grade>().HasQueryFilter(g => CurrentTenantId == null || g.Submission.InstitutionId == CurrentTenantId);
        m.Entity<ActivityLog>().HasQueryFilter(a => CurrentTenantId == null || a.Session.InstitutionId == CurrentTenantId);
        m.Entity<RefreshToken>().HasQueryFilter(rt => CurrentTenantId == null || rt.User.InstitutionId == CurrentTenantId);
        m.Entity<PasswordResetRequest>().HasQueryFilter(r => CurrentTenantId == null || r.InstitutionId == CurrentTenantId);
    }
}