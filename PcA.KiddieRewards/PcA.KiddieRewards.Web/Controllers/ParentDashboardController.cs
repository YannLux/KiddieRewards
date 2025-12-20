using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
public class ParentDashboardController(AppDbContext dbContext, IDashboardService dashboardService, IPointsService pointsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var viewModel = await dashboardService.BuildParentDashboardAsync(familyId, cancellationToken);

        // Explicitly point to the existing view under Views/Parent/Dashboard.cshtml
        return View("~/Views/Parent/Dashboard.cshtml", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAllChildren(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out var memberId))
        {
            return Forbid();
        }

        var childIds = await dbContext.Members
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.Role == MemberRole.Child && m.IsActive)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (!childIds.Any())
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var reason = $"Reset {DateTime.Now:dd/MM/yyyy HH:mm}";
        await pointsService.AddPointsAsync(familyId, memberId, childIds, PointEntryType.Reset, 0, reason, cancellationToken);

        return RedirectToAction(nameof(Dashboard));
    }

    private bool TryGetFamilyId(out Guid familyId)
    {
        var success = TryGetFamilyAndMember(out familyId, out _);
        return success;
    }

    private bool TryGetFamilyAndMember(out Guid familyId, out Guid memberId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            familyId = Guid.Empty;
            memberId = Guid.Empty;
            return false;
        }

        var familyClaim = User.FindFirst("FamilyId")?.Value;
        var memberClaim = User.FindFirst("MemberId")?.Value;

        if (Guid.TryParse(familyClaim, out var claimFamilyId) && Guid.TryParse(memberClaim, out var claimMemberId))
        {
            familyId = claimFamilyId;
            memberId = claimMemberId;
            return true;
        }

        var member = dbContext.Members
            .AsNoTracking()
            .FirstOrDefault(m => m.Id == userGuid);

        if (member is not null)
        {
            familyId = member.FamilyId;
            memberId = member.Id;
            return true;
        }

        familyId = Guid.Empty;
        memberId = Guid.Empty;
        return false;
    }
}
