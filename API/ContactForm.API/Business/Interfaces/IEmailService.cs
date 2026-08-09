using ContactForm.API.Models;

namespace ContactForm.API.Business.Interfaces;

public interface IEmailService
{
    Task SendContactMessageAsync(ContactRequest request, CancellationToken ct = default);
}