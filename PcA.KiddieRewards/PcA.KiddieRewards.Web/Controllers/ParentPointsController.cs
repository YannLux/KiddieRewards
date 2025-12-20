using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
public class ParentPointsController(AppDbContext dbContext, IPointsService pointsService, ISuggestionsService suggestionsService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> AddPoints(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out _))
        {
            return Forbid();
        }

        var viewModel = await BuildAddPointsViewModel(familyId, new AddPointsViewModel());
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPoints(AddPointsViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out var createdByMemberId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildAddPointsViewModel(familyId, model);
            return View(invalidModel);
        }

        await pointsService.AddPointsAsync(familyId, createdByMemberId, model.ChildMemberIds, model.Type, model.Points, model.Reason, cancellationToken);
        return RedirectToAction("Dashboard", "ParentDashboard");
    }

    [HttpGet]
    public async Task<IActionResult> EditPoint(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out _))
        {
            return Forbid();
        }

        var entry = await dbContext.PointEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.FamilyId == familyId, cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        var viewModel = new EditPointViewModel
        {
            PointEntryId = entry.Id,
            Points = entry.Points,
            Reason = entry.Reason,
            Type = entry.Type,
            IsActive = entry.IsActive
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPoint(EditPointViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out _))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var entry = await dbContext.PointEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.PointEntryId && p.FamilyId == familyId, cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        await pointsService.UpdatePointEntryAsync(entry.Id, model.Type, model.Points, model.Reason, model.IsActive, cancellationToken);
        return RedirectToAction("Dashboard", "ParentDashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Suggestions(string? term, int limit = 10, CancellationToken cancellationToken = default)
    {
        if (!TryGetFamilyAndMember(out var familyId, out _))
        {
            return Forbid();
        }

        var suggestions = await suggestionsService.GetSuggestionsAsync(familyId, term, limit, cancellationToken);
        return Json(suggestions);
    }

    private async Task<AddPointsViewModel> BuildAddPointsViewModel(Guid familyId, AddPointsViewModel model)
    {
        var children = await dbContext.Members
            .AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.Role == MemberRole.Child && m.IsActive)
            .OrderBy(m => m.DisplayName)
            .ToListAsync();

        return model with
        {
            AvailableChildren = children.Select(c => new MemberOption(c.Id, c.DisplayName)).ToList()
        };
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

public record MemberOption(Guid Id, string DisplayName);

public record AddPointsViewModel : IValidatableObject
{
    [Required]
    public List<Guid> ChildMemberIds { get; init; } = new();

    [Range(-10000, 10000)]
    [Required]
    public int Points { get; init; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    public PointEntryType Type { get; init; } = PointEntryType.GoodPoint;

    public IReadOnlyList<MemberOption> AvailableChildren { get; init; } = new List<MemberOption>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == PointEntryType.Reset)
        {
            yield break;
        }

        var absolutePoints = Math.Abs(Points);

        if (absolutePoints == 0)
        {
            yield return new ValidationResult("La valeur doit être strictement supérieure à zéro. Le signe saisi sera ignoré et ajusté selon le type choisi.", new[] { nameof(Points) });
        }
    }
}

public record EditPointViewModel : IValidatableObject
{
    [Required]
    public Guid PointEntryId { get; init; }

    [Range(-10000, 10000)]
    [Required]
    public int Points { get; init; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    [Required]
    public PointEntryType Type { get; init; } = PointEntryType.GoodPoint;

    public bool? IsActive { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == PointEntryType.Reset)
        {
            yield break;
        }

        var absolutePoints = Math.Abs(Points);

        if (absolutePoints == 0)
        {
            yield return new ValidationResult("La valeur doit être strictement supérieure à zéro. Le signe saisi sera ignoré et ajusté selon le type choisi.", new[] { nameof(Points) });
        }
    }
}
