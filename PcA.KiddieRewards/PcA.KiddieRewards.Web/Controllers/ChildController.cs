using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Child")]
public class ChildController(AppDbContext dbContext, IPointsService pointsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out var memberId))
        {
            return Forbid();
        }

        var child = await dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId && m.Role == MemberRole.Child && m.IsActive, cancellationToken);

        if (child is null)
        {
            return NotFound();
        }

        var totals = await pointsService.GetTotalsAsync(memberId, cancellationToken);
        var history = await pointsService.GetHistoryAsync(memberId, cancellationToken: cancellationToken);

        var viewModel = new ChildMeViewModel(child.DisplayName, child.AvatarKey, totals, history);
        return View(viewModel);
    }

    private bool TryGetFamilyAndMember(out Guid familyId, out Guid memberId)
    {
        var familyClaim = User.FindFirst("FamilyId")?.Value;
        var memberClaim = User.FindFirst("MemberId")?.Value;

        var hasFamily = Guid.TryParse(familyClaim, out familyId);
        var hasMember = Guid.TryParse(memberClaim, out memberId);

        return hasFamily && hasMember;
    }
}

public record ChildMeViewModel(string DisplayName, string AvatarKey, PointsTotals Totals, IReadOnlyList<PointEntry> History);
