namespace ContactForm.API.Business.Interfaces;

public interface ICaptchaService
{
    Task<bool> VerifyAsync(string? token, string remoteIp, CancellationToken ct = default);
}