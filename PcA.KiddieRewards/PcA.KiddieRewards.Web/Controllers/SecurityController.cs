using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Constants;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers;

[Authorize]
public class SecurityController(AppDbContext dbContext, IPinHasher pinHasher, IMemberSignInService memberSignInService, UserManager<IdentityUser> userManager) : Controller
{
    [HttpGet]
    public async Task<IActionResult> EnterPin(string? returnUrl, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Forbid();
        }

        var member = await dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == userGuid && m.IsActive, cancellationToken);

        if (member is null)
        {
            return RedirectToAction("OnboardingFamily", "Home");
        }

        var sessionUserId = HttpContext.Session.GetString(SessionKeys.PinValidatedUserId);
        if (sessionUserId == userId)
        {
            return RedirectToLocal(returnUrl);
        }

        var viewModel = new VerifyPinViewModel
        {
            DisplayName = member.DisplayName,
            ReturnUrl = returnUrl
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnterPin(VerifyPinViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Forbid();
        }

        var member = await dbContext.Members
            .FirstOrDefaultAsync(m => m.Id == userGuid && m.IsActive, cancellationToken);

        if (member is null)
        {
            ModelState.AddModelError(nameof(model.Pin), "Aucun membre associé à ce compte.");
            return View(model);
        }

        if (!pinHasher.VerifyPin(member.PinHash, model.Pin))
        {
            ModelState.AddModelError(nameof(model.Pin), "PIN incorrect.");
            return View(model);
        }

        var identityUser = await userManager.FindByIdAsync(userId);
        if (identityUser is null)
        {
            return Forbid();
        }

        await memberSignInService.SignInAsync(identityUser, member, HttpContext);

        return RedirectToLocal(model.ReturnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}

public record VerifyPinViewModel
{
    public string? DisplayName { get; init; }

    [Required]
    [StringLength(10, MinimumLength = 4)]
    [RegularExpression(@"^\d+$", ErrorMessage = "Le PIN doit contenir uniquement des chiffres.")]
    public string Pin { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }
}
