using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
public class ParentSettingsController(AppDbContext dbContext, IPointsService pointsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var family = await dbContext.Families
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == familyId, cancellationToken);

        if (family is null)
        {
            return NotFound();
        }

        var viewModel = new FamilySettingsViewModel
        {
            Name = family.Name,
            IsActive = family.IsActive
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(FamilySettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var family = await dbContext.Families.FirstOrDefaultAsync(f => f.Id == familyId, cancellationToken);

        if (family is null)
        {
            return NotFound();
        }

        family.Name = model.Name.Trim();
        family.IsActive = model.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerReset(ResetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out var createdByMemberId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Settings));
        }

        var childExists = await dbContext.Members
            .AsNoTracking()
            .AnyAsync(m => m.Id == request.ChildMemberId && m.FamilyId == familyId && m.Role == MemberRole.Child, cancellationToken);

        if (!childExists)
        {
            return NotFound();
        }

        await pointsService.ApplyResetAsync(familyId, request.ChildMemberId, createdByMemberId, request.Reason, cancellationToken);
        return RedirectToAction("Dashboard", "ParentDashboard");
    }

    private bool TryGetFamilyId(out Guid familyId)
    {
        var familyClaim = User.FindFirst("FamilyId")?.Value;
        return Guid.TryParse(familyClaim, out familyId);
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

public record FamilySettingsViewModel
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public record ResetRequest
{
    [Required]
    public Guid ChildMemberId { get; init; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;
}
