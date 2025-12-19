using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
public class ParentDashboardController(AppDbContext dbContext, IDashboardService dashboardService) : Controller
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
