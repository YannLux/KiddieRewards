using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
public class ParentDashboardController(AppDbContext dbContext, IPointsService pointsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var children = await dbContext.Members
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.Role == MemberRole.Child && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);

        var childSummaries = new List<ChildDashboardItem>();

        foreach (var child in children)
        {
            var totals = await pointsService.GetTotalsAsync(child.Id, cancellationToken);
            childSummaries.Add(new ChildDashboardItem(child.Id, child.DisplayName, totals.Plus, totals.Minus, totals.Net));
        }

        var viewModel = new ParentDashboardViewModel(familyId, childSummaries);
        return View(viewModel);
    }

    private bool TryGetFamilyId(out Guid familyId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            familyId = Guid.Empty;
            return false;
        }

        // Try to get from claim first (for compatibility)
        var familyClaim = User.FindFirst("FamilyId")?.Value;
        if (Guid.TryParse(familyClaim, out var claimFamilyId))
        {
            familyId = claimFamilyId;
            return true;
        }

        // Otherwise, query the database to find the user's family
        var member = dbContext.Members
            .AsNoTracking()
            .FirstOrDefault(m => m.Id == userGuid);

        if (member is not null)
        {
            familyId = member.FamilyId;
            return true;
        }

        familyId = Guid.Empty;
        return false;
    }
}

public record ChildDashboardItem(Guid MemberId, string DisplayName, int Plus, int Minus, int Net);

public record ParentDashboardViewModel(Guid FamilyId, IReadOnlyList<ChildDashboardItem> Children);
