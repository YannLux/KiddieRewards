using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
[Route("Parent")]
public class ParentChildController(AppDbContext dbContext, IPinHasher pinHasher) : Controller
{
    [HttpGet("ChildDetails")]
    public async Task<IActionResult> ChildDetails(Guid? editId, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var viewModel = await BuildViewModelAsync(familyId, editId, null, cancellationToken);
        return View("~/Views/Parent/ChildDetails.cshtml", viewModel);
    }

    [HttpPost("CreateChild")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateChild([Bind(Prefix = "Form")] EditChildViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        ValidatePin(model.Pin, required: true);

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildViewModelAsync(familyId, null, model, cancellationToken);
            return View("~/Views/Parent/ChildDetails.cshtml", invalidModel);
        }

        var child = new Member
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            DisplayName = model.DisplayName.Trim(),
            AvatarKey = model.AvatarKey.Trim(),
            PinHash = pinHasher.HashPin(model.Pin),
            Role = MemberRole.Child,
            IsActive = model.IsActive
        };

        dbContext.Members.Add(child);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(ChildDetails));
    }

    [HttpPost("UpdateChild/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateChild(Guid id, [Bind(Prefix = "Form")] EditChildViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(model.Pin))
        {
            ValidatePin(model.Pin, required: false);
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildViewModelAsync(familyId, id, model, cancellationToken);
            return View("~/Views/Parent/ChildDetails.cshtml", invalidModel);
        }

        var child = await dbContext.Members.FirstOrDefaultAsync(
            m => m.Id == id && m.FamilyId == familyId && m.Role == MemberRole.Child,
            cancellationToken);

        if (child is null)
        {
            return NotFound();
        }

        child.DisplayName = model.DisplayName.Trim();
        child.AvatarKey = model.AvatarKey.Trim();
        child.IsActive = model.IsActive;

        if (!string.IsNullOrWhiteSpace(model.Pin))
        {
            child.PinHash = pinHasher.HashPin(model.Pin);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(ChildDetails));
    }

    [HttpPost("ToggleChild/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleChild(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var child = await dbContext.Members.FirstOrDefaultAsync(
            m => m.Id == id && m.FamilyId == familyId && m.Role == MemberRole.Child,
            cancellationToken);

        if (child is null)
        {
            return NotFound();
        }

        child.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(ChildDetails));
    }

    private async Task<ChildDetailsPageViewModel> BuildViewModelAsync(
        Guid familyId,
        Guid? editChildId,
        EditChildViewModel? formModel,
        CancellationToken cancellationToken)
    {
        var children = await dbContext.Members
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.Role == MemberRole.Child)
            .OrderBy(m => m.DisplayName)
            .Select(m => new ChildSummaryViewModel(m.Id, m.DisplayName, m.AvatarKey, m.IsActive))
            .ToListAsync(cancellationToken);

        EditChildViewModel resolvedForm;

        if (formModel is not null)
        {
            resolvedForm = formModel;
        }
        else if (editChildId.HasValue)
        {
            var editing = children.FirstOrDefault(c => c.Id == editChildId.Value);
            resolvedForm = editing is null
                ? new EditChildViewModel()
                : new EditChildViewModel
                {
                    DisplayName = editing.DisplayName,
                    AvatarKey = editing.AvatarKey,
                    IsActive = editing.IsActive
                };
        }
        else
        {
            resolvedForm = new EditChildViewModel();
        }

        return new ChildDetailsPageViewModel
        {
            Children = children,
            Form = resolvedForm,
            EditingChildId = editChildId
        };
    }

    private void ValidatePin(string pin, bool required)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            if (required)
            {
            ModelState.AddModelError("Form.Pin", "Le PIN est requis (4 à 10 caractères).");
            }

            return;
        }

        if (pin.Length is < 4 or > 10)
        {
            ModelState.AddModelError("Form.Pin", "Le PIN doit contenir entre 4 et 10 caractères.");
        }
    }

    private bool TryGetFamilyId(out Guid familyId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            familyId = Guid.Empty;
            return false;
        }

        var familyClaim = User.FindFirst("FamilyId")?.Value;
        if (Guid.TryParse(familyClaim, out var claimFamilyId))
        {
            familyId = claimFamilyId;
            return true;
        }

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

public record ChildDetailsPageViewModel
{
    public IReadOnlyList<ChildSummaryViewModel> Children { get; init; } = new List<ChildSummaryViewModel>();

    public EditChildViewModel Form { get; init; } = new();

    public Guid? EditingChildId { get; init; }

    public bool IsEditMode => EditingChildId.HasValue;
}

public record ChildSummaryViewModel(Guid Id, string DisplayName, string AvatarKey, bool IsActive);

public record EditChildViewModel
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AvatarKey { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    [MaxLength(10)]
    public string Pin { get; init; } = string.Empty;
}
