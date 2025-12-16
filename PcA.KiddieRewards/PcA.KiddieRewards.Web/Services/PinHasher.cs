using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace PcA.KiddieRewards.Web.Services;

public interface IPinHasher
{
    string HashPin(string pin);

    PasswordVerificationResult VerifyHashedPin(string hashedPin, string providedPin);
}

public sealed class PinHasher : IPinHasher
{
    private readonly IPasswordHasher<object> _passwordHasher;
    private readonly object _sharedUser = new();

    public PinHasher() : this(null)
    {
    }

    public PinHasher(IOptions<PasswordHasherOptions>? optionsAccessor)
    {
        _passwordHasher = optionsAccessor is null
            ? new PasswordHasher<object>()
            : new PasswordHasher<object>(optionsAccessor);
    }

    public string HashPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ArgumentException("PIN must not be empty.", nameof(pin));
        }

        return _passwordHasher.HashPassword(_sharedUser, pin);
    }

    public PasswordVerificationResult VerifyHashedPin(string hashedPin, string providedPin)
    {
        if (hashedPin is null)
        {
            throw new ArgumentNullException(nameof(hashedPin));
        }

        if (string.IsNullOrWhiteSpace(providedPin))
        {
            return PasswordVerificationResult.Failed;
        }

        return _passwordHasher.VerifyHashedPassword(_sharedUser, hashedPin, providedPin);
    }
}
