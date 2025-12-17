using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
public class ParentMembersController(AppDbContext dbContext, IPinHasher pinHasher) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var members = await dbContext.Members
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(cancellationToken);

        return View(members);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new EditMemberViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EditMemberViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.Pin) || model.Pin.Length < 4 || model.Pin.Length > 10)
        {
            ModelState.AddModelError(nameof(model.Pin), "PIN must be between 4 and 10 characters.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var member = new Member
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            DisplayName = model.DisplayName.Trim(),
            AvatarKey = model.AvatarKey.Trim(),
            PinHash = pinHasher.HashPin(model.Pin),
            Role = model.Role,
            IsActive = model.IsActive
        };

        dbContext.Members.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var member = await dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id && m.FamilyId == familyId, cancellationToken);

        if (member is null)
        {
            return NotFound();
        }

        var viewModel = new EditMemberViewModel
        {
            DisplayName = member.DisplayName,
            AvatarKey = member.AvatarKey,
            Role = member.Role,
            IsActive = member.IsActive
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EditMemberViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(model.Pin) && (model.Pin.Length < 4 || model.Pin.Length > 10))
        {
            ModelState.AddModelError(nameof(model.Pin), "PIN must be between 4 and 10 characters.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var member = await dbContext.Members.FirstOrDefaultAsync(m => m.Id == id && m.FamilyId == familyId, cancellationToken);

        if (member is null)
        {
            return NotFound();
        }

        member.DisplayName = model.DisplayName.Trim();
        member.AvatarKey = model.AvatarKey.Trim();
        member.Role = model.Role;
        member.IsActive = model.IsActive;

        if (!string.IsNullOrWhiteSpace(model.Pin))
        {
            member.PinHash = pinHasher.HashPin(model.Pin);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
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

public record EditMemberViewModel
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AvatarKey { get; init; } = string.Empty;

    [Required]
    public MemberRole Role { get; init; } = MemberRole.Child;

    public bool IsActive { get; init; } = true;

    [MaxLength(10)]
    public string Pin { get; init; } = string.Empty;
}
