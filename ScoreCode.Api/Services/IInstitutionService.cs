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

    /// <summary>
    /// Platform admin: approve a pending institution, provisioning its first
    /// institution-admin account. Returns the credentials once, or null if the
    /// institution is missing or not pending.
    /// </summary>
    Task<ApproveInstitutionResponse?> ApproveAsync(Guid institutionId);

    /// <summary>Platform admin: reject a pending institution with a reason.</summary>
    Task<bool> RejectAsync(Guid institutionId, string reason);
}
