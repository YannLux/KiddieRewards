using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize(Roles = "Parent")]
public class ParentInvitationsController(AppDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out _))
        {
            return Forbid();
        }

        var invitations = await dbContext.FamilyInvitations
            .AsNoTracking()
            .Where(i => i.FamilyId == familyId)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new ParentInvitationItem(
                i.Id,
                i.Code,
                i.CreatedAtUtc,
                i.ExpiresAtUtc,
                i.IsRevoked,
                i.RedeemedAtUtc))
            .ToListAsync(cancellationToken);

        var viewModel = new ParentInvitationsViewModel(invitations);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!TryGetFamilyAndMember(out var familyId, out var memberId))
        {
            return Forbid();
        }

        var invitation = new FamilyInvitation
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            Code = await GenerateUniqueCodeAsync(cancellationToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedByMemberId = memberId
        };

        dbContext.FamilyInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetFamilyId(out var familyId))
        {
            return Forbid();
        }

        var invitation = await dbContext.FamilyInvitations
            .FirstOrDefaultAsync(i => i.Id == id && i.FamilyId == familyId, cancellationToken);

        if (invitation is null)
        {
            return NotFound();
        }

        invitation.IsRevoked = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
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

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        const string charset = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        const int length = 10;

        using var rng = RandomNumberGenerator.Create();

        while (true)
        {
            var code = GenerateCode(rng, charset, length);
            var exists = await dbContext.FamilyInvitations.AnyAsync(i => i.Code == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }
    }

    private static string GenerateCode(RandomNumberGenerator rng, string charset, int length)
    {
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = charset[bytes[i] % charset.Length];
        }

        return new string(chars);
    }
}

public record ParentInvitationsViewModel(IReadOnlyList<ParentInvitationItem> Invitations);

public record ParentInvitationItem(
    Guid Id,
    string Code,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsRevoked,
    DateTime? RedeemedAtUtc)
{
    public bool IsUsed => RedeemedAtUtc is not null;

    public bool IsExpired => DateTime.UtcNow > ExpiresAtUtc;

    public bool IsActive => !IsRevoked && !IsUsed && !IsExpired;
}
