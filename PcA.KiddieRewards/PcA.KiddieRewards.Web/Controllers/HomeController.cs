using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;
using PcA.KiddieRewards.Web.Services;

namespace PcA.KiddieRewards.Web.Controllers
{
    public class HomeController(AppDbContext dbContext, IPinHasher pinHasher, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<IdentityUser> signInManager, IPointsService pointsService) : Controller
    {
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            // If user is not authenticated, show public landing page
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                return View();
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return View();
            }

            // Check if user has any family membership
            var userHasFamily = await dbContext.Members
                .AsNoTracking()
                .AnyAsync(m => m.Id == userGuid, cancellationToken);

            if (!userHasFamily)
            {
                return RedirectToAction(nameof(OnboardingFamily));
            }

            // Determine selected family: cookie -> validate -> fallback to first family
            Guid selectedFamilyId = Guid.Empty;
            if (Request.Cookies.TryGetValue("SelectedFamilyId", out var cookieValue) && Guid.TryParse(cookieValue, out var cookieGuid))
            {
                // validate membership
                var isMember = await dbContext.Members
                    .AsNoTracking()
                    .AnyAsync(m => m.Id == userGuid && m.FamilyId == cookieGuid, cancellationToken);

                if (isMember)
                {
                    selectedFamilyId = cookieGuid;
                }
            }

            if (selectedFamilyId == Guid.Empty)
            {
                // fallback to the first family for this user
                var member = await dbContext.Members
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == userGuid, cancellationToken);

                if (member is null)
                {
                    return RedirectToAction(nameof(OnboardingFamily));
                }

                selectedFamilyId = member.FamilyId;
                // persist cookie
                Response.Cookies.Append("SelectedFamilyId", selectedFamilyId.ToString(), new CookieOptions { HttpOnly = false });
            }

            // Build same view model as ParentDashboardController
            var children = await dbContext.Members
                .AsNoTracking()
                .Where(m => m.FamilyId == selectedFamilyId && m.Role == MemberRole.Child && m.IsActive)
                .OrderBy(m => m.DisplayName)
                .ToListAsync(cancellationToken);

            var childSummaries = new List<ChildDashboardItem>();
            foreach (var child in children)
            {
                var totals = await pointsService.GetTotalsAsync(child.Id, cancellationToken);
                childSummaries.Add(new ChildDashboardItem(child.Id, child.DisplayName, totals.Plus, totals.Minus, totals.Net));
            }

            var viewModel = new ParentDashboardViewModel(selectedFamilyId, childSummaries);

            // Reuse existing Parent dashboard view if present
            return View("~/Views/Parent/Dashboard.cshtml", viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public IActionResult OnboardingFamily()
        {
            return View();
        }

        [Authorize]
        [HttpGet]
        public IActionResult CreateFamily()
        {
            return View(new CreateFamilyViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFamily(CreateFamilyViewModel model, CancellationToken cancellationToken)
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

            // Vérifier que l'utilisateur n'a pas déjà une famille
            var existingMember = await dbContext.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == userGuid, cancellationToken);
            
            if (existingMember != null)
            {
                ModelState.AddModelError("", "Vous avez déjà une famille assignée.");
                return View(model);
            }

            var family = new Family
            {
                Id = Guid.NewGuid(),
                Name = model.FamilyName.Trim()
            };

            var member = new Member
            {
                Id = userGuid,
                FamilyId = family.Id,
                DisplayName = model.ParentDisplayName.Trim(),
                AvatarKey = model.AvatarKey?.Trim() ?? "parent-star",
                PinHash = pinHasher.HashPin(model.ParentPin),
                Role = MemberRole.Parent,
                IsActive = true
            };

            dbContext.Families.Add(family);
            dbContext.Members.Add(member);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Ensure Identity role and assign to user, then refresh sign-in so role is effective immediately
            var identityUser = await userManager.FindByIdAsync(userGuid.ToString());
            if (identityUser is not null)
            {
                const string parentRole = "Parent";
                if (!await roleManager.RoleExistsAsync(parentRole))
                {
                    await roleManager.CreateAsync(new IdentityRole(parentRole));
                }

                if (!await userManager.IsInRoleAsync(identityUser, parentRole))
                {
                    await userManager.AddToRoleAsync(identityUser, parentRole);
                }

                await signInManager.RefreshSignInAsync(identityUser);
            }

            // Set selected family cookie
            Response.Cookies.Append("SelectedFamilyId", family.Id.ToString(), new CookieOptions { HttpOnly = false });

            return RedirectToAction("Dashboard", "ParentDashboard");
        }

        [Authorize]
        [HttpGet]
        public IActionResult JoinFamily()
        {
            return View(new JoinFamilyViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinFamily(JoinFamilyViewModel model, CancellationToken cancellationToken)
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

            // Vérifier que l'utilisateur n'a pas déjà une famille
            var existingMember = await dbContext.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == userGuid, cancellationToken);
            
            if (existingMember != null)
            {
                ModelState.AddModelError("", "Vous avez déjà une famille assignée.");
                return View(model);
            }

            // Pour MVP: utiliser le code d'invitation comme clé pour retrouver la famille
            // En production, cela devrait être un système plus sécurisé
            var family = await dbContext.Families
                .FirstOrDefaultAsync(f => f.Name == model.FamilyInvitationCode, cancellationToken);

            if (family is null)
            {
                ModelState.AddModelError(nameof(model.FamilyInvitationCode), "Code d'invitation invalide.");
                return View(model);
            }

            var member = new Member
            {
                Id = userGuid,
                FamilyId = family.Id,
                DisplayName = model.DisplayName.Trim(),
                AvatarKey = model.AvatarKey?.Trim() ?? "parent-star",
                PinHash = pinHasher.HashPin(model.Pin),
                Role = MemberRole.Parent,
                IsActive = true
            };

            dbContext.Members.Add(member);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Assign Identity role and refresh sign-in
            var identityUser = await userManager.FindByIdAsync(userGuid.ToString());
            if (identityUser is not null)
            {
                const string parentRole = "Parent";
                if (!await roleManager.RoleExistsAsync(parentRole))
                {
                    await roleManager.CreateAsync(new IdentityRole(parentRole));
                }

                if (!await userManager.IsInRoleAsync(identityUser, parentRole))
                {
                    await userManager.AddToRoleAsync(identityUser, parentRole);
                }

                await signInManager.RefreshSignInAsync(identityUser);
            }

            // Set selected family cookie
            Response.Cookies.Append("SelectedFamilyId", family.Id.ToString(), new CookieOptions { HttpOnly = false });

            return RedirectToAction("Dashboard", "ParentDashboard");
        }

        [Authorize]
        [HttpGet]
        public IActionResult SelectFamily(Guid id, string? returnUrl)
        {
            // Validate the family exists and belongs to the user
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
            {
                return Forbid();
            }

            var member = dbContext.Members.AsNoTracking().FirstOrDefault(m => m.Id == userGuid && m.FamilyId == id);
            if (member is null)
            {
                return Forbid();
            }

            // Set cookie to remember selected family
            Response.Cookies.Append("SelectedFamilyId", id.ToString(), new CookieOptions { HttpOnly = false });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public record CreateFamilyViewModel
    {
        [Required(ErrorMessage = "Le nom de la famille est requis.")]
        [MaxLength(200, ErrorMessage = "Le nom ne peut pas dépasser 200 caractères.")]
        public string FamilyName { get; init; } = string.Empty;

        [Required(ErrorMessage = "Votre nom est requis.")]
        [MaxLength(100, ErrorMessage = "Votre nom ne peut pas dépasser 100 caractères.")]
        public string ParentDisplayName { get; init; } = string.Empty;

        [Required(ErrorMessage = "Le code PIN est requis.")]
        [StringLength(10, MinimumLength = 4, ErrorMessage = "Le PIN doit être entre 4 et 10 caractères.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Le PIN ne doit contenir que des chiffres.")]
        public string ParentPin { get; init; } = string.Empty;

        [MaxLength(100)]
        public string? AvatarKey { get; init; }
    }

    public record JoinFamilyViewModel
    {
        [Required(ErrorMessage = "Le code d'invitation est requis.")]
        public string FamilyInvitationCode { get; init; } = string.Empty;

        [Required(ErrorMessage = "Votre nom est requis.")]
        [MaxLength(100, ErrorMessage = "Votre nom ne peut pas dépasser 100 caractères.")]
        public string DisplayName { get; init; } = string.Empty;

        [Required(ErrorMessage = "Le code PIN est requis.")]
        [StringLength(10, MinimumLength = 4, ErrorMessage = "Le PIN doit être entre 4 et 10 caractères.")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Le PIN ne doit contenir que des chiffres.")]
        public string Pin { get; init; } = string.Empty;

        [MaxLength(100)]
        public string? AvatarKey { get; init; }
    }
}
