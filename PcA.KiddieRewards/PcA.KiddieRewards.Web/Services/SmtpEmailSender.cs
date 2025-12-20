using System.IO;
using System.Net;
using System.Net.Mail;
using System.Linq;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PcA.KiddieRewards.Web.Services;

public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;
    private readonly ILogger<SmtpEmailSender> _logger = logger;

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlMessage);

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("SMTP host is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.From))
        {
            throw new InvalidOperationException("SMTP sender address is not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.From, _options.FromDisplayName ?? _options.From),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };

        message.To.Add(email);

        if (await TrySendWithSmtpAsync(message))
        {
            return;
        }

        if (_options.FallbackToPickupDirectory)
        {
            await SendToPickupDirectoryAsync(message);
            return;
        }

        throw new InvalidOperationException("Failed to send email via SMTP and pickup directory fallback is disabled.");
    }

    private async Task<bool> TrySendWithSmtpAsync(MailMessage message)
    {
        var recipients = string.Join(", ", message.To.Select(r => r.Address));

        try
        {
            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            client.UseDefaultCredentials = _options.UseDefaultCredentials;

            if (!_options.UseDefaultCredentials && !string.IsNullOrWhiteSpace(_options.UserName))
            {
                client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
            }

            await client.SendMailAsync(message);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "Unable to send email via SMTP host {Host}:{Port}.", _options.Host, _options.Port);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "SMTP connection failed while sending email to {Recipients}.", recipients);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "SMTP configuration error while sending email to {Recipients}.", recipients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected SMTP failure while sending email to {Recipients}.", recipients);
        }

        return false;
    }

    private async Task SendToPickupDirectoryAsync(MailMessage message)
    {
        var pickupDirectory = ResolvePickupDirectory();

        _logger.LogWarning("Falling back to pickup directory at {PickupDirectory}.", pickupDirectory);

        using var pickupClient = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupDirectory
        };

        await pickupClient.SendMailAsync(message);
    }

    private string ResolvePickupDirectory()
    {
        var directory = !string.IsNullOrWhiteSpace(_options.PickupDirectoryLocation)
            ? _options.PickupDirectoryLocation
            : Path.Combine(AppContext.BaseDirectory, "mail-drop");

        Directory.CreateDirectory(directory);

        return directory;
    }
}
