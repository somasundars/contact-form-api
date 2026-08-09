using System.Threading.RateLimiting;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using Amazon.SimpleEmailV2;
using ContactForm.API.Business.Implementations;
using ContactForm.API.Business.Interfaces;
using ContactForm.API.Business.Services;
using ContactForm.API.Middleware;
using ContactForm.API.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// True both when actually running in Lambda and left false for local `dotnet run`,
// so the exact same code/binary serves both environments (recommended pattern).
var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

if (isLambda)
{
    // Wires the app into the Lambda runtime via API Gateway HTTP API (v2 payloads).
    builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

    // Resolve Email:PasswordSecretArn / Captcha:SecretKeyArn (set by Terraform) into
    // real values from Secrets Manager, once, at cold start.
    await SecretsResolver.ResolveAsync(builder);
}

// ---- Configuration-bound settings ----
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<CaptchaSettings>(builder.Configuration.GetSection("Captcha"));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("Cors:AllowedOrigins must be configured");

// ---- CORS ----
// Locally (Kestrel) the app enforces it directly. In Lambda, API Gateway's native
// CORS handling (see terraform/api_gateway.tf) answers preflight OPTIONS requests
// before they even reach Lambda, which is both cheaper and simpler than doing it
// twice — so app-level CORS is skipped there to avoid duplicate/conflicting headers.
const string CorsPolicyName = "ContactFormCors";
if (!isLambda)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicyName, policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("POST")
                  .WithHeaders("Content-Type")
                  .DisallowCredentials();
        });
    });
}

// ---- Rate limiting ----
// NOTE: in Lambda this only limits requests hitting the *same warm execution
// environment* — it is not a global limiter across concurrent invocations, since
// Lambda scales horizontally with separate memory per instance. Kept here as
// cheap defense-in-depth; the real, global limit is enforced at API Gateway
// (throttling_burst_limit / throttling_rate_limit in terraform), optionally
// backed by an AWS WAF rate-based rule for stronger protection.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("ContactFormPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));
});

builder.Services.AddHttpClient<ICaptchaService, CaptchaService>();

// ---- Email provider: SES (recommended for Lambda) or SMTP ----
var emailProvider = builder.Configuration["Email:Provider"] ?? "Smtp";
if (string.Equals(emailProvider, "Ses", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(new AmazonSimpleEmailServiceV2Client());
    builder.Services.AddScoped<IEmailService, SesEmailService>();
}
else
{
    builder.Services.AddScoped<IEmailService, EmailService>();
}

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o => o.SuppressMapClientErrors = false);

if (!isLambda)
{
    // Kestrel-specific: irrelevant in Lambda (API Gateway enforces its own 10 MB
    // payload cap), but the [RequestSizeLimit] attribute on the controller action
    // applies in both environments regardless.
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024);

    builder.Services.AddHsts(o =>
    {
        o.MaxAge = TimeSpan.FromDays(365);
        o.IncludeSubDomains = true;
    });
}

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errApp =>
    {
        errApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "An unexpected error occurred." });
        });
    });
}

if (!isLambda)
{
    // Behind API Gateway, TLS is already terminated at the edge (execute-api /
    // custom domain endpoints are HTTPS-only), so these are redundant there and
    // risk a redirect loop if scheme detection ever misfires.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

if (!isLambda)
{
    app.UseCors(CorsPolicyName);
}

app.UseRateLimiter();
app.UseAuthorization();

var controllers = app.MapControllers().RequireRateLimiting("ContactFormPolicy");
if (!isLambda)
{
    controllers.RequireCors(CorsPolicyName);
}

app.Run();