using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using PcA.KiddieRewards.Web.Constants;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Services;

public interface IMemberSignInService
{
    Task SignInAsync(IdentityUser identityUser, Member member, HttpContext httpContext);
}

public class MemberSignInService(SignInManager<IdentityUser> signInManager) : IMemberSignInService
{
    public async Task SignInAsync(IdentityUser identityUser, Member member, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(identityUser);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(httpContext);

        var claims = new List<Claim>
        {
            new("FamilyId", member.FamilyId.ToString()),
            new("MemberId", member.Id.ToString()),
            new("DisplayName", member.DisplayName),
            new(ClaimTypes.Role, member.Role.ToString()),
            new(ClaimTypes.Name, member.DisplayName)
        };

        await signInManager.SignInWithClaimsAsync(identityUser, isPersistent: false, claims);
        httpContext.Session.SetString(SessionKeys.PinValidatedUserId, identityUser.Id);
    }
}
