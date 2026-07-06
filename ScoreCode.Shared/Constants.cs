namespace ScoreCode.Shared;

public static class Constants
{
    public static class Roles
    {
        public const string Admin = "Admin";               // legacy single-tenant admin (== platform for now)
        public const string PlatformAdmin = "PlatformAdmin"; // owns the platform; approves institutions (no tenant)
        public const string InstitutionAdmin = "InstitutionAdmin"; // admin of one institution
        public const string Lecturer = "Lecturer";
        public const string Student = "Student";

        // For [Authorize(Roles = ...)] — either platform role may manage institutions.
        public const string PlatformOrAdmin = PlatformAdmin + "," + Admin;
        // Either platform-level admin or an institution admin (tenant-scoped management).
        public const string AnyAdmin = PlatformAdmin + "," + Admin + "," + InstitutionAdmin;
    }

    /// <summary>Lifecycle states stored in <c>Institution.Status</c>.</summary>
    public static class InstitutionStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Suspended = "Suspended";
    }

    public static class ErrorCodes
    {
        public const string InvalidCredentials = "invalid_credentials";
        public const string InvalidToken = "invalid_token";
        public const string TokenExpired = "token_expired";
        public const string ExamNotFound = "exam_not_found";
        public const string ExamNotActive = "exam_not_active";
        public const string ExamAlreadyPublished = "exam_already_published";
        public const string StudentNotAssigned = "student_not_assigned";
        public const string SessionAlreadyActive = "session_already_active";
        public const string HashMismatch = "hash_mismatch";
        public const string SubmissionNotFound = "submission_not_found";
        public const string ResultsNotReleased = "results_not_released";
        public const string AiServiceUnavailable = "ai_service_unavailable";
        public const string OverrideReasonRequired = "override_reason_required";
        public const string EmailTaken = "email_taken";
        public const string UserNotFound = "user_not_found";
        public const string NoQuestionsExist = "no_questions";
        public const string NoStudentsAssigned = "no_students";
        public const string ExamNotOpen = "exam_not_open";
        public const string ExamWindowClosed = "exam_window_closed";
        public const string SessionNotFound = "session_not_found";
        public const string UngradedSubmissionsExist = "ungraded_submissions";
        public const string InvalidInstitution = "invalid_institution";
        public const string NoInstitutionContext = "no_institution_context";
    }

    /// <summary>Lifecycle states stored in <c>Exam.Status</c>.</summary>
    public static class ExamStatuses
    {
        public const string Draft = "Draft";   // being authored; editable
        public const string Active = "Active";  // published/live (auto or manual)
        public const string Closed = "Closed";  // ended; no new sessions
    }

    /// <summary>Lifecycle states stored in <c>Session.Status</c>.</summary>
    public static class SessionStatuses
    {
        public const string Active = "Active";
        public const string Completed = "Completed";
    }

    public static class Exam
    {
        /// <summary>
        /// How long after an exam's scheduled start a student may still begin it.
        /// After this window a brand-new session is refused; an already-active
        /// session can still be resumed. Students keep the full duration once started.
        /// </summary>
        public const int StartGraceMinutes = 15;
    }

    /// <summary>Anti-cheat / session event types recorded to ActivityLog.</summary>
    public static class ActivityEvents
    {
        public const string FocusLost = "focus_lost";
        public const string BlockedKey = "blocked_key";
        public const string ExamStarted = "exam_started";
        public const string ExamSubmitted = "exam_submitted";
    }

    public static class SignalR
    {
        public const string ExamStarted = "ExamStarted";
        public const string ExamEnded = "ExamEnded";
        public const string TimerSync = "TimerSync";
        public const string CheatEvent = "CheatEvent";
        public const string SubmissionReceived = "SubmissionReceived";
        public const string TimeExtended = "TimeExtended";
    }

    public static class Academic
    {
        public const string Department = "Computing Sciences";

        public static readonly string[] Programmes =
        [
            "Software Engineering",
            "Computer Science",
            "Cyber Security"
        ];

        public static readonly string[] Levels =
        [
            "100", "200", "300", "400", "500", "Postgrad"
        ];
    }
}