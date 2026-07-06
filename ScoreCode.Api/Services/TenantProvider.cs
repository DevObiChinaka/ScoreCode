using System.Security.Claims;
using ScoreCode.Database;
using ScoreCode.Shared;

namespace ScoreCode.Api.Services;

/// <summary>
/// Resolves the current tenant from the authenticated JWT for the DbContext's
/// global query filters. Platform admins and anonymous/startup requests get
/// null (no scoping); a tenant user missing the institution claim gets
/// Guid.Empty (matches nothing) so isolation fails safe rather than open.
/// </summary>
public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _accessor;

    public TenantProvider(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? CurrentInstitutionId
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                return null; // anonymous (login/register) or no HTTP context (startup/jobs)

            var role = user.FindFirstValue(ClaimTypes.Role);
            if (role == Constants.Roles.PlatformAdmin || role == Constants.Roles.Admin)
                return null; // platform-level: sees all tenants

            var claim = user.FindFirstValue("institutionId");
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }
}
