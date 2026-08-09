namespace ContactForm.API.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        // This is a pure JSON API (no HTML/script served), so a locked-down CSP
        // is safe and mainly future-proofs against any accidental HTML responses.
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        await _next(context);
    }
}