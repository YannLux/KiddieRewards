using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PcA.KiddieRewards.Web.Data;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Services;

public interface IPinAuthService
{
    Task<ClaimsPrincipal?> AuthenticateAsync(Guid memberId, string pin, CancellationToken cancellationToken = default);
}

public class PinAuthService(AppDbContext dbContext, IPinHasher pinHasher) : IPinAuthService
{
    public async Task<ClaimsPrincipal?> AuthenticateAsync(Guid memberId, string pin, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);

        var member = await dbContext.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.IsActive, cancellationToken);

        if (member is null)
        {
            return null;
        }

        if (!pinHasher.VerifyPin(member.PinHash, pin))
        {
            return null;
        }

        return BuildPrincipal(member);
    }

    private static ClaimsPrincipal BuildPrincipal(Member member)
    {
        var claims = new List<Claim>
        {
            new("MemberId", member.Id.ToString()),
            new("FamilyId", member.FamilyId.ToString()),
            new(ClaimTypes.Role, member.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "Pin");
        return new ClaimsPrincipal(identity);
    }
}
