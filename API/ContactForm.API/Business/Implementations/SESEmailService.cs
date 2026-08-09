using System.Net;
using System.Text.RegularExpressions;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using ContactForm.API.Business.Interfaces;
using ContactForm.API.Models;
using Microsoft.Extensions.Options;

namespace ContactForm.API.Business.Implementations;

/// <summary>
/// Sends via Amazon SES instead of raw SMTP. Preferred for the Lambda deployment:
/// no SMTP username/password to store or rotate at all — auth is via the Lambda
/// execution role's IAM permissions (ses:SendEmail), and it isn't affected by
/// AWS's outbound SMTP port restrictions on new/unverified accounts.
/// Requires the FromAddress's domain (or the address itself) to be a verified
/// SES identity — see terraform/ses.tf.
/// </summary>
public class SesEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly ILogger<SesEmailService> _logger;

    private static readonly Regex ControlChars = new(@"[\r\n\0]", RegexOptions.Compiled);

    public SesEmailService(IOptions<EmailSettings> settings, IAmazonSimpleEmailServiceV2 ses, ILogger<SesEmailService> logger)
    {
        _settings = settings.Value;
        _ses = ses;
        _logger = logger;
    }

    public async Task SendContactMessageAsync(ContactRequest request, CancellationToken ct = default)
    {
        string Clean(string? s) => ControlChars.Replace(s ?? string.Empty, string.Empty).Trim();

        var safeName = Clean(request.Name);
        var safeEmail = Clean(request.Email);
        var safeSubject = Clean(request.Subject);
        var safeMessage = Clean(request.Message);

        var subject = string.IsNullOrWhiteSpace(safeSubject)
            ? "New contact form submission"
            : $"Contact form: {safeSubject}";

        var textBody = $"Name: {safeName}\nEmail: {safeEmail}\n\n{safeMessage}";
        var htmlBody = $"<p><strong>Name:</strong> {WebUtility.HtmlEncode(safeName)}</p>" +
                        $"<p><strong>Email:</strong> {WebUtility.HtmlEncode(safeEmail)}</p>" +
                        $"<p>{WebUtility.HtmlEncode(safeMessage).Replace("\n", "<br/>")}</p>";

        var sendRequest = new SendEmailRequest
        {
            FromEmailAddress = _settings.FromAddress,
            Destination = new Destination { ToAddresses = new List<string> { _settings.ToAddress } },
            // Visitor's address goes in Reply-To only — never as the SES sending identity,
            // which SES would reject anyway unless that address is separately verified.
            ReplyToAddresses = new List<string> { safeEmail },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject, Charset = "UTF-8" },
                    Body = new Body
                    {
                        Text = new Content { Data = textBody, Charset = "UTF-8" },
                        Html = new Content { Data = htmlBody, Charset = "UTF-8" }
                    }
                }
            }
        };

        await _ses.SendEmailAsync(sendRequest, ct);
        _logger.LogInformation("Contact form message sent via SES from {Email}", safeEmail);
    }
}