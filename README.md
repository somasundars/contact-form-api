# Contact Form API

A minimal, hardened ASP.NET Core 8 Web API for a public/anonymous contact form.

## Setup

```bash
dotnet restore
dotnet user-secrets init
dotnet user-secrets set "Email:Password" "your-smtp-password"
dotnet user-secrets set "Captcha:SecretKey" "your-captcha-secret"   # if using a captcha
```

Edit `appsettings.json`:

- `Cors:AllowedOrigins` — set to your real site origin(s), e.g. `https://www.yourdomain.com`. Only these origins can call the API from a browser.
- `Email:*` — your SMTP relay details (SendGrid, SES, Mailgun, your host's SMTP, etc).

Run locally:

```bash
dotnet run
```

## Security features included

| Feature                             | Where                                 | Why                                                                                                    |
| ----------------------------------- | ------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| CORS locked to your domain          | `Program.cs`                          | Browsers block calls from any other origin                                                             |
| Rate limiting (5 req / 10 min / IP) | `Program.cs`, `ContactController`     | Blunts scripted spam floods                                                                            |
| Request body size cap (32 KB)       | `Program.cs`, `ContactController`     | Blocks oversized/DoS payloads                                                                          |
| Honeypot field                      | `ContactRequest`, `ContactController` | Silently drops basic bot submissions                                                                   |
| Cloudflare Turnstile                | `CaptchaService`                      | Stops bots more sophisticated than the honeypot catches — **recommended**, set `Captcha:Enabled: true` |
| Strict model validation             | `ContactRequest` (Data Annotations)   | Rejects malformed/oversized fields before any processing                                               |
| Email header injection protection   | `EmailService`                        | Strips CR/LF from all fields; visitor address goes in Reply-To, never From                             |
| HTML-encoded email body             | `EmailService`                        | Prevents stored/rendered XSS if you (or staff) view the message as HTML                                |
| No exception detail leakage         | `Program.cs`, `ContactController`     | Generic error responses; details only in server logs                                                   |
| Security response headers + CSP     | `SecurityHeadersMiddleware`           | Defense-in-depth against MIME sniffing, framing, etc.                                                  |
| HTTPS redirection + HSTS            | `Program.cs`                          | Forces TLS in production                                                                               |
| Forwarded headers support           | `Program.cs`                          | Correct client IP for rate limiting behind a reverse proxy/load balancer                               |

## Deployment notes

- **Always run behind HTTPS.** Terminate TLS at your host/proxy or let Kestrel handle it directly.
- **Secrets**: never commit real SMTP/captcha credentials. Use `dotnet user-secrets` locally and environment variables or a secret manager (Azure Key Vault, AWS Secrets Manager, etc.) in production.
- **Enable Turnstile** (`Captcha:Enabled: true`) once you've added the widget to your frontend form (see below and get a site key/secret key at the [Cloudflare Turnstile dashboard](https://dash.cloudflare.com/?to=/:account/turnstile)). This is the single biggest anti-spam upgrade available for a fully anonymous, public-facing form. Set `Captcha:ExpectedHostname` to your real domain too — it stops a token solved on someone else's site from being replayed against yours.
- **Reverse proxy**: if you deploy behind IIS/Nginx/a load balancer, make sure it also enforces a body size limit and forwards `X-Forwarded-For` correctly (already configured to trust it in `Program.cs` — tighten `ForwardedHeadersOptions` further, e.g. `KnownProxies`, if you want to be strict about which proxy is trusted).
- **Logging**: submission emails are logged only by address, not full message content, to avoid dumping PII into logs. Adjust to your compliance needs.

## Deploying to AWS Lambda with Terraform

The same codebase runs locally (Kestrel) and on Lambda — `Program.cs` detects the
environment at startup (`AWS_LAMBDA_FUNCTION_NAME` env var) and adjusts:

- **Hosting**: `Amazon.Lambda.AspNetCoreServer.Hosting` wires the app to API Gateway HTTP API (v2 payload) events.
- **CORS**: handled natively by API Gateway instead of the app, so preflight `OPTIONS` never invokes Lambda.
- **Rate limiting**: API Gateway's `throttling_rate_limit`/`throttling_burst_limit` is the real, global control. The in-process ASP.NET Core limiter only protects a single warm Lambda instance, since Lambda scales horizontally with separate memory per instance — it stays on as cheap defense-in-depth, not the primary control.
- **TLS**: `UseHttpsRedirection`/`UseHsts` are skipped in Lambda since API Gateway endpoints are HTTPS-only already.
- **Email**: defaults to **Amazon SES** instead of SMTP — no credentials to store at all (auth is via the Lambda execution role's IAM permissions), and it sidesteps AWS's outbound SMTP port restrictions on newer accounts. SMTP is still available via `email_provider = "smtp"`.
- **Secrets**: the CAPTCHA key (and SMTP password, if used) are stored in Secrets Manager. Terraform passes only the secret _ARN_ as a Lambda env var; the app resolves the real value once at cold start (`SecretsResolver.cs`) — so nothing sensitive ever sits in the Lambda console's plaintext environment variables.

### Deploy

```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars   # edit with your real values
export TF_VAR_captcha_secret_key="..."          # if captcha_enabled = true
# export TF_VAR_email_password="..."            # only if email_provider = "smtp"

terraform init
terraform apply
```

This also runs `dotnet publish` for you (via a `local-exec` provisioner triggered on source changes) and zips the output — you don't need a separate build step. Requires the .NET 8 SDK on the machine running `terraform apply`.

### Before your first real send: verify SES

With `email_provider = "ses"` (default), SES will reject sends until the `email_from` address's domain is verified. Set `manage_ses_domain_identity = true` and `ses_domain` in your tfvars — `terraform apply` will output DKIM CNAME records to add at your DNS provider. Also note new AWS accounts start in the SES _sandbox_, which only allows sending to verified addresses; request production access in the SES console before going live.

### What Terraform creates

- Lambda function (arm64, `dotnet8` runtime) behind an execution role scoped to: CloudWatch Logs, `secretsmanager:GetSecretValue` on just its own secrets, and (if `email_provider = ses`) `ses:SendEmail` restricted to the configured `From` address.
- API Gateway HTTP API with a single `POST /api/contact` route (no open proxy route), native CORS, throttling, and access logging.
- Secrets Manager secrets for the CAPTCHA key and/or SMTP password.

## Frontend integration

Add the Turnstile widget to your form (get a site key from the [Cloudflare Turnstile dashboard](https://dash.cloudflare.com/?to=/:account/turnstile) — use a different site key for local/staging than production, since site keys are tied to specific hostnames):

```html
<script
  src="https://challenges.cloudflare.com/turnstile/v0/api.js"
  async
  defer
></script>

<form id="contact-form">
  <input name="name" required />
  <input name="email" type="email" required />
  <input name="subject" />
  <textarea name="message" required></textarea>

  <!-- Honeypot: keep it in the DOM but visually hidden, not display:none
       (some bots skip display:none fields) -->
  <input
    name="website"
    style="position:absolute; left:-9999px"
    tabindex="-1"
    autocomplete="off"
  />

  <div class="cf-turnstile" data-sitekey="YOUR_SITE_KEY"></div>

  <button type="submit">Send</button>
</form>
```

```js
document
  .getElementById("contact-form")
  .addEventListener("submit", async (e) => {
    e.preventDefault();
    const form = e.target;

    await fetch("https://api.yourdomain.com/api/contact", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        name: form.name.value,
        email: form.email.value,
        subject: form.subject.value,
        message: form.message.value,
        honeypotField: form.website.value, // stays empty for real users
        captchaToken: turnstile.getResponse(), // populated by the widget once solved
      }),
    });
  });
```

Turnstile tokens are single-use and expire after a few minutes, so if a submission fails and the user retries, call `turnstile.reset()` before resubmitting or the second `siteverify` call will fail with `timeout-or-duplicate`.
