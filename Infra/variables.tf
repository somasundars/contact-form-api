variable "project_name" {
  description = "Prefix used for all created resource names"
  type        = string
  default     = "contact-form-api"
}

variable "aws_region" {
  type    = string
  default = "us-east-1"
}

variable "allowed_origins" {
  description = "Your site origin(s) allowed to call the API from a browser, e.g. [\"https://www.yourdomain.com\"]"
  type        = list(string)
}

variable "custom_domain_name" {
  description = "Custom domain name for the API Gateway"
  type        = string
  default     = ""
}

variable "route53_zone_name" {
  description = "Route 53 hosted zone name that will contain the custom domain record"
  type        = string
  default     = ""
}

variable "lambda_memory_size" {
  type    = number
  default = 256
}

variable "lambda_timeout" {
  type    = number
  default = 10
}

# ---- API Gateway throttling: the real, global rate limit (Lambda instances
# don't share the in-process ASP.NET Core limiter's state) ----
variable "throttling_burst_limit" {
  type    = number
  default = 10
}

variable "throttling_rate_limit" {
  type    = number
  default = 5 # steady-state requests/sec across the whole API
}

# ---- Email ----
variable "email_provider" {
  description = "\"ses\" (recommended) or \"smtp\""
  type        = string
  default     = "ses"
}

variable "email_from" {
  description = "Must be a verified SES identity (or its domain) if email_provider = ses"
  type        = string
}

variable "email_to" {
  type = string
}

# SMTP-only settings (ignored when email_provider = "ses")
variable "email_host" {
  type    = string
  default = ""
}

variable "email_port" {
  type    = number
  default = 587
}

variable "email_username" {
  type    = string
  default = ""
}

variable "email_password" {
  description = "SMTP password. Sensitive — pass via TF_VAR_email_password env var or an untracked *.auto.tfvars, never commit it. Ignored when email_provider = ses."
  type        = string
  default     = ""
  sensitive   = true
}

variable "email_allow_invalid_ssl_certificate" {
  description = "SMTP-only: if true, allows sending to an SMTP server with an invalid SSL certificate. Ignored when email_provider = ses."
  type        = bool
  default     = false
}

# ---- Captcha (recommended for an anonymous public form) ----
variable "captcha_enabled" {
  type    = bool
  default = false
}

variable "captcha_verify_url" {
  type    = string
  default = "https://challenges.cloudflare.com/turnstile/v0/siteverify"
}

variable "captcha_expected_hostname" {
  description = "Your site hostname, e.g. www.yourdomain.com — checked against the hostname Turnstile echoes back, to stop token replay from other sites. Leave blank to skip."
  type        = string
  default     = ""
}

variable "captcha_secret_key" {
  description = "Sensitive — pass via TF_VAR_captcha_secret_key env var, never commit it."
  type        = string
  default     = ""
  sensitive   = true
}

# ---- Optional: verify a domain identity in SES via Terraform ----
variable "manage_ses_domain_identity" {
  description = "If true, creates an aws_sesv2_email_identity for ses_domain and prints the DNS records you must add to verify it"
  type        = bool
  default     = false
}

variable "ses_domain" {
  description = "Domain to verify in SES, e.g. yourdomain.com (only used if manage_ses_domain_identity = true)"
  type        = string
  default     = ""
}
