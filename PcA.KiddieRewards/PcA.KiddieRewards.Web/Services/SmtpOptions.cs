using System.ComponentModel.DataAnnotations;

namespace PcA.KiddieRewards.Web.Services;

public record SmtpOptions
{
    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    [Required]
    public string From { get; init; } = string.Empty;

    public string? FromDisplayName { get; init; }

    public bool EnableSsl { get; init; } = true;

    public string? UserName { get; init; }

    public string? Password { get; init; }

    public bool UseDefaultCredentials { get; init; }

    /// <summary>
    /// When true, outgoing emails are written to a pickup directory if SMTP delivery fails.
    /// </summary>
    public bool FallbackToPickupDirectory { get; init; } = true;

    /// <summary>
    /// Optional location for the pickup directory. Defaults to a "mail-drop" folder under the application base path.
    /// </summary>
    public string? PickupDirectoryLocation { get; init; }
}
