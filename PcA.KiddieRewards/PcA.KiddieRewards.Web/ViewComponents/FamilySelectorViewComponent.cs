using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.ViewComponents;

public class FamilySelectorViewComponent : ViewComponent
{
    private readonly AppDbContext _dbContext;

    public FamilySelectorViewComponent(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = HttpContext.User;
        if (user?.Identity is null || !user.Identity.IsAuthenticated)
        {
            return Content(string.Empty);
        }

        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Content(string.Empty);
        }

        var families = await _dbContext.Members
            .AsNoTracking()
            .Where(m => m.Id == userGuid)
            .Include(m => m.Family)
            .Select(m => m.Family)
            .Where(f => f != null)
            .Distinct()
            .ToListAsync();

        return View(families);
    }
}
