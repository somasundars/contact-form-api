using System.Net;
using System.Text.RegularExpressions;
using ContactForm.API.Business.Interfaces;
using ContactForm.API.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ContactForm.API.Business.Implementations;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    // CR/LF (and other control chars) in header-bound fields is the classic
    // "email header injection" vector — it lets an attacker smuggle extra
    // Bcc:/Subject:/etc. headers into the message. We strip them defensively
    // even though MimeKit itself also refuses raw header injection.
    private static readonly Regex ControlChars = new(@"[\r\n\0]", RegexOptions.Compiled);

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendContactMessageAsync(ContactRequest request, CancellationToken ct = default)
    {
        string Clean(string? s) => ControlChars.Replace(s ?? string.Empty, string.Empty).Trim();

        var safeName = Clean(request.Name);
        var safeEmail = Clean(request.Email);
        var safeSubject = Clean(request.Subject);
        var safeMessage = Clean(request.Message);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Website Contact Form", _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(_settings.ToAddress));

        // The visitor's address goes in Reply-To, never From/Sender — putting
        // untrusted input directly into From lets attackers spoof your domain
        // and can break SPF/DKIM alignment.
        if (MailboxAddress.TryParse(safeEmail, out var replyTo))
        {
            message.ReplyTo.Add(replyTo);
        }

        message.Subject = string.IsNullOrWhiteSpace(safeSubject)
            ? "New contact form submission"
            : $"Contact form: {safeSubject}";

        // BodyBuilder + TextPart handles MIME-safe encoding; we also HTML-encode
        // for the HTML alternative so injected markup/script can't render if the
        // message is viewed as HTML in a webmail client.
        var builder = new BodyBuilder
        {
            TextBody = $"Name: {safeName}\nEmail: {safeEmail}\n\n{safeMessage}",
            HtmlBody = $"<p><strong>Name:</strong> {WebUtility.HtmlEncode(safeName)}</p>" +
                       $"<p><strong>Email:</strong> {WebUtility.HtmlEncode(safeEmail)}</p>" +
                       $"<p>{WebUtility.HtmlEncode(safeMessage).Replace("\n", "<br/>")}</p>"
        };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Contact form message sent from {Email}", safeEmail);
    }
}