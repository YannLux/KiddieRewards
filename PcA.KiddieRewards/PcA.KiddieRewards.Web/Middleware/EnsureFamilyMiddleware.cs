using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;

namespace PcA.KiddieRewards.Web.Middleware;

public class EnsureFamilyMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        // Always skip for non-GET requests (static assets are GET/HEAD)
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await next(context);
            return;
        }

        // Skip middleware for non-authenticated users
        if (!(context.User?.Identity?.IsAuthenticated ?? false))
        {
            await next(context);
            return;
        }

        // Skip middleware for certain routes or static asset folders
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        if (path.StartsWith("/identity") ||
            path.StartsWith("/home/onboarding") ||
            path.StartsWith("/home/createfamily") ||
            path.StartsWith("/home/joinfamily") ||
            path.StartsWith("/home/privacy") ||
            path.StartsWith("/api") ||
            path.StartsWith("/lib/") ||
            path.StartsWith("/css/") ||
            path.StartsWith("/js/") ||
            path.StartsWith("/images/") ||
            path.StartsWith("/img/") ||
            path.StartsWith("/favicon.ico") ||
            path.StartsWith("/_framework") ||
            path.Contains(".css") ||
            path.Contains(".js") ||
            path.Contains(".map") ||
            path.Contains(".png") ||
            path.Contains(".jpg") ||
            path.Contains(".jpeg") ||
            path.Contains(".svg") ||
            path.Contains(".woff") ||
            path.Contains(".woff2") ||
            path.Contains(".ttf")
            )
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(userId) && Guid.TryParse(userId, out var userGuid))
        {
            var userHasFamily = await dbContext.Members
                .AsNoTracking()
                .AnyAsync(m => m.Id == userGuid);

            if (!userHasFamily)
            {
                context.Response.Redirect("/Home/OnboardingFamily");
                return;
            }
        }

        await next(context);
    }
}

public static class EnsureFamilyMiddlewareExtensions
{
    public static IApplicationBuilder UseEnsureFamily(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<EnsureFamilyMiddleware>();
    }
}
