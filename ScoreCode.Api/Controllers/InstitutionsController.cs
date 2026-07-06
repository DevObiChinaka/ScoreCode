using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScoreCode.Api.Services;
using ScoreCode.Shared;
using ScoreCode.Shared.DTOs;
using ScoreCode.Shared.DTOs.Institutions;

namespace ScoreCode.Api.Controllers;

[ApiController]
[Route("api/v1/institutions")]
public class InstitutionsController : ControllerBase
{
    private readonly IInstitutionService _svc;

    public InstitutionsController(IInstitutionService svc) => _svc = svc;

    // POST /api/v1/institutions/apply — public application
    [HttpPost("apply")]
    [AllowAnonymous]
    public async Task<IActionResult> Apply([FromBody] InstitutionApplicationRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.ContactEmail))
            return BadRequest(ApiResponse<object>.Fail("validation_failed",
                "Institution name and a contact email are required."));

        var result = await _svc.ApplyAsync(req);
        return StatusCode(201, ApiResponse<InstitutionDto>.Ok(result));
    }

    // GET /api/v1/institutions/options — public list for the register dropdown
    [HttpGet("options")]
    [AllowAnonymous]
    public async Task<IActionResult> Options()
    {
        var list = await _svc.GetApprovedOptionsAsync();
        return Ok(ApiResponse<List<InstitutionOption>>.Ok(list));
    }

    // GET /api/v1/institutions?status=Pending — platform admin
    [HttpGet]
    [Authorize(Roles = Constants.Roles.PlatformOrAdmin)]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var list = await _svc.GetAllAsync(status);
        return Ok(ApiResponse<List<InstitutionDto>>.Ok(list));
    }

    // POST /api/v1/institutions/{id}/approve — platform admin
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Constants.Roles.PlatformOrAdmin)]
    public async Task<IActionResult> Approve(Guid id)
    {
        var result = await _svc.ApproveAsync(id);
        return result is null
            ? BadRequest(ApiResponse<object>.Fail("not_pending",
                "Institution not found or not awaiting approval."))
            : Ok(ApiResponse<ApproveInstitutionResponse>.Ok(result));
    }

    // POST /api/v1/institutions/{id}/reject — platform admin
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Constants.Roles.PlatformOrAdmin)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectInstitutionRequest req)
    {
        var ok = await _svc.RejectAsync(id, req.Reason);
        return ok
            ? Ok(ApiResponse<object>.Ok(new { }))
            : BadRequest(ApiResponse<object>.Fail("not_pending",
                "Institution not found or not awaiting approval."));
    }
}
