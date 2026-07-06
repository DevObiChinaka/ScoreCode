namespace ScoreCode.Database;

/// <summary>
/// Supplies the current request's tenant so the DbContext can apply global
/// query filters. Implemented in the API layer from the authenticated JWT.
///
/// Semantics of <see cref="CurrentInstitutionId"/>:
///   • null            -> no scoping (platform admin, background jobs, anonymous/login)
///   • a real Guid     -> scope every tenant-owned query to that institution
///   • Guid.Empty      -> a tenant user with no institution claim: match nothing (fail-safe)
/// </summary>
public interface ITenantProvider
{
    Guid? CurrentInstitutionId { get; }
}
