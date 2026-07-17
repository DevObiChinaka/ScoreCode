using ScoreCode.Shared.DTOs.Institutions;

namespace ScoreCode.Api.Services;

public interface IInstitutionService
{
    /// <summary>Public: submit an institution application (status Pending).</summary>
    Task<InstitutionDto> ApplyAsync(InstitutionApplicationRequest req);

    /// <summary>Public: approved institutions for the student register dropdown.</summary>
    Task<List<InstitutionOption>> GetApprovedOptionsAsync();

    /// <summary>Platform admin: list institutions, optionally filtered by status.</summary>
    Task<List<InstitutionDto>> GetAllAsync(string? status);

    /// <summary>Platform admin: full drill-down for one institution (admin, counts, recent users).</summary>
    Task<InstitutionDetailDto?> GetDetailAsync(Guid institutionId);

    /// <summary>
    /// Platform admin: approve a pending institution, provisioning its first
    /// institution-admin account. Returns the credentials once, or null if the
    /// institution is missing or not pending.
    /// </summary>
    Task<ApproveInstitutionResponse?> ApproveAsync(Guid institutionId);

    /// <summary>
    /// Platform admin: edit an institution's details. The prefix is only applied
    /// while the institution has no users, since IDs are derived from it.
    /// Returns the updated record, or an error message if it could not be saved.
    /// </summary>
    Task<(InstitutionDto? Institution, string? Error)> UpdateAsync(Guid institutionId, UpdateInstitutionRequest req);

    /// <summary>Platform admin: reject a pending institution with a reason.</summary>
    Task<bool> RejectAsync(Guid institutionId, string reason);

    /// <summary>Platform admin: suspend an approved institution (removes it from the register dropdown).</summary>
    Task<bool> SuspendAsync(Guid institutionId);

    /// <summary>Platform admin: reactivate a suspended institution.</summary>
    Task<bool> ReactivateAsync(Guid institutionId);
}
