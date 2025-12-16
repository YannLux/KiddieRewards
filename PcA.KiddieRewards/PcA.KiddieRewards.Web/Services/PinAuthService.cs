using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace PcA.KiddieRewards.Web.Services;

public enum MemberRole
{
    Parent,
    Child
}

public sealed record MemberPinProfile(int MemberId, int FamilyId, MemberRole Role, string PinHash);

public interface IPinAuthService
{
    bool TryAuthenticate(MemberPinProfile member, string providedPin, out ClaimsPrincipal? principal);
}

public sealed class PinAuthService(IPinHasher pinHasher) : IPinAuthService
{
    private readonly IPinHasher _pinHasher = pinHasher ?? throw new ArgumentNullException(nameof(pinHasher));

    public bool TryAuthenticate(MemberPinProfile member, string providedPin, out ClaimsPrincipal? principal)
    {
        principal = null;

        ArgumentNullException.ThrowIfNull(member);

        var verification = _pinHasher.VerifyHashedPin(member.PinHash, providedPin);
        if (verification is not (PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded))
        {
            return false;
        }

        var claims = new List<Claim>
        {
            new("MemberId", member.MemberId.ToString()),
            new("FamilyId", member.FamilyId.ToString()),
            new(ClaimTypes.Role, member.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "Pin");
        principal = new ClaimsPrincipal(identity);
        return true;
    }
}
