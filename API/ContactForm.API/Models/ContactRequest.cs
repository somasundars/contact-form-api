using System.ComponentModel.DataAnnotations;

namespace ContactForm.API.Models
{
    public class ContactRequest
    {
        [Required]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string Name { get; set; } = null!;
        [Required]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = null!;
        [Required]
        [StringLength(100, ErrorMessage = "Subject cannot exceed 100 characters.")]
        public string Subject { get; set; } = null!;
        [Required]
        [StringLength(4000, ErrorMessage = "Message cannot exceed 4000 characters.")]
        public string Message { get; set; } = null!;
        public string? HoneypotField { get; set; } // Hidden field for bot detection

        [Required]
        public string CaptchaToken { get; set; } = null!;
    }
}