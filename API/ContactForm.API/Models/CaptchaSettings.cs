namespace ContactForm.API.Models
{

    public class CaptchaSettings
    {
        public bool Enabled { get; set; } = false;
        public string SecretKey { get; set; } = string.Empty;
        /// <summary>ARN of a Secrets Manager secret holding the captcha secret key (Lambda only). See SecretsResolver.</summary>
        public string? SecretKeyArn { get; set; }

        // hCaptcha, Cloudflare Turnstile, and reCAPTCHA all expose a compatible
        // siteverify endpoint shape (POST secret/response/remoteip -> {success}).
        public string VerifyUrl { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        /// <summary>
        /// Optional but recommended: your site's hostname, e.g. "www.yourdomain.com".
        /// Turnstile's siteverify response echoes back the hostname the token was
        /// issued for — checking it here stops a token solved on someone else's
        /// site (or a dev/staging domain) from being replayed against prod.
        /// Leave blank to skip this check.
        /// </summary>
        public string? ExpectedHostname { get; set; }
    }
}