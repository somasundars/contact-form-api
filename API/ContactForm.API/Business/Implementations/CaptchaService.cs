using ContactForm.API.Business.Interfaces;
using ContactForm.API.Models;
using Microsoft.Extensions.Options;

namespace ContactForm.API.Business.Implementations;


/// <summary>
/// Strongly recommended for a public, anonymous, unauthenticated contact form —
/// honeypots catch simple bots, but a captcha is what stops scripted/human-assisted
/// spam floods. Defaults to Cloudflare Turnstile's siteverify endpoint.
/// Disabled by default so the API runs without extra setup; set
/// Captcha:Enabled = true and Captcha:SecretKey once you've added the Turnstile
/// widget to your frontend form.
/// </summary>
public class CaptchaService : ICaptchaService
{
    private readonly CaptchaSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CaptchaService> _logger;

    public CaptchaService(IOptions<CaptchaSettings> settings, HttpClient httpClient, ILogger<CaptchaService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string? token, string remoteIp, CancellationToken ct = default)
    {
        if (!_settings.Enabled)
        {
            return true; // captcha not configured — skip (honeypot + rate limiting still apply)
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var response = await _httpClient.PostAsync(_settings.VerifyUrl,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = _settings.SecretKey,
                    ["response"] = token,
                    ["remoteip"] = remoteIp
                }), ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile siteverify returned {Status}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileVerifyResult>(cancellationToken: ct);

            if (result is null || !result.Success)
            {
                // error-codes are non-sensitive (things like "invalid-input-response",
                // "timeout-or-duplicate") — safe and useful to log for debugging.
                _logger.LogInformation("Turnstile verification failed: {Errors}",
                    result?.ErrorCodes is { Length: > 0 } errs ? string.Join(",", errs) : "unknown");
                return false;
            }

            // Defense against token replay across sites/environments.
            if (!string.IsNullOrWhiteSpace(_settings.ExpectedHostname) &&
                !string.Equals(result.Hostname, _settings.ExpectedHostname, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Turnstile token hostname mismatch: expected {Expected}, got {Actual}",
                    _settings.ExpectedHostname, result.Hostname);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Captcha verification request failed");
            return false; // fail closed — treat verification errors as failure
        }
    }

    private class TurnstileVerifyResult
    {
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }

        public string? Hostname { get; set; }
        public string? Action { get; set; }
    }
}