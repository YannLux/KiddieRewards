using Microsoft.AspNetCore.Identity;
using PcA.KiddieRewards.Web.Models;

namespace PcA.KiddieRewards.Web.Services;

public interface IPinHasher
{
    string HashPin(string pin);

    bool VerifyPin(string hash, string pin);
}

public class PinHasher(IPasswordHasher<Member> passwordHasher) : IPinHasher
{
    public string HashPin(string pin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);

        return passwordHasher.HashPassword(new Member(), pin);
    }

    public bool VerifyPin(string hash, string pin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);

        var result = passwordHasher.VerifyHashedPassword(new Member(), hash, pin);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
