using ContactForm.API.Business.Interfaces;
using ContactForm.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ContactFormApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("ContactFormPolicy")]
public class ContactController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ICaptchaService _captchaService;
    private readonly ILogger<ContactController> _logger;

    public ContactController(IEmailService emailService, ICaptchaService captchaService, ILogger<ContactController> logger)
    {
        _emailService = emailService;
        _captchaService = captchaService;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(32 * 1024)] // 32 KB — defense in depth alongside the Kestrel-level limit
    public async Task<IActionResult> Submit([FromBody] ContactRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Honeypot: bots fill every field, real users never see this one.
        // Return a fake "success" instead of an error so bots don't learn
        // to adapt — this is a deliberate silent-drop, not a bug.
        if (!string.IsNullOrEmpty(request.HoneypotField))
        {
            _logger.LogInformation("Honeypot triggered from {Ip}", remoteIp);
            return Ok(new { message = "Thanks! Your message has been sent." });
        }

        var captchaOk = await _captchaService.VerifyAsync(request.CaptchaToken, remoteIp, ct);
        if (!captchaOk)
        {
            return BadRequest(new { message = "Captcha verification failed. Please try again." });
        }

        try
        {
            await _emailService.SendContactMessageAsync(request, ct);
        }
        catch (Exception ex)
        {
            // Never leak SMTP/internal exception details to the client.
            _logger.LogError(ex, "Failed to send contact form email from {Ip}", remoteIp);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Sorry, we couldn't send your message right now. Please try again shortly." });
        }

        return Ok(new { message = "Thanks! Your message has been sent." });
    }
}