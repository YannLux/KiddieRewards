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
        var familyClaim = User.FindFirst("FamilyId")?.Value;
        return Guid.TryParse(familyClaim, out familyId);
    }
}

public record ChildDashboardItem(Guid MemberId, string DisplayName, int Plus, int Minus, int Net);

public record ParentDashboardViewModel(Guid FamilyId, IReadOnlyList<ChildDashboardItem> Children);
