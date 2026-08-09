namespace ContactForm.API.Models
{

    public class EmailSettings
    {
        /// <summary>"Smtp" (default) or "Ses". Set Email:Provider = "Ses" for the Lambda deployment.</summary>
        public string Provider { get; set; } = "Smtp";
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        /// <summary>ARN of a Secrets Manager secret holding the SMTP password (Lambda only). See SecretsResolver.</summary>
        public string? PasswordSecretArn { get; set; }
        public string FromAddress { get; set; } = string.Empty;
        public string ToAddress { get; set; } = string.Empty;
    }
}