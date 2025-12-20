using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Constants;
using PcA.KiddieRewards.Web.Data;

namespace PcA.KiddieRewards.Web.Middleware;

public class RequirePinMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            await next(context);
            return;
        }

        if (!(context.User?.Identity?.IsAuthenticated ?? false))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (IsPathExcluded(path))
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await next(context);
            return;
        }

        var validatedUserId = context.Session.GetString(SessionKeys.PinValidatedUserId);
        if (validatedUserId == userId)
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            await next(context);
            return;
        }

        var hasMember = await dbContext.Members
            .AsNoTracking()
            .AnyAsync(m => m.Id == userGuid && m.IsActive);

        if (!hasMember)
        {
            await next(context);
            return;
        }

        var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
        context.Response.Redirect($"/Security/EnterPin?returnUrl={returnUrl}");
        return;
    }

    private static bool IsPathExcluded(string path) =>
        path.StartsWith("/security/enterpin") ||
        path.StartsWith("/identity") ||
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
        path.Contains(".ttf");
}

public static class RequirePinMiddlewareExtensions
{
    public static IApplicationBuilder UseRequirePin(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequirePinMiddleware>();
    }
}
