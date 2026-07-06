namespace ScoreCode.Shared.Enums;

/// <summary>
/// Exam lifecycle states. Persisted as strings — the canonical values live in
/// <see cref="Constants.ExamStatuses"/>, which is what the data layer compares
/// against. There is no separate "Published" state: publishing (manual or the
/// scheduled auto-publish) moves an exam straight from Draft to Active.
/// </summary>
public enum ExamStatus
{
    Draft,
    Active,
    Closed
}