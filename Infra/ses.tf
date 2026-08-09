# Optional convenience: creates the SES domain identity via Terraform. SES still
# requires you to manually add the printed DNS records (or use a
# aws_route53_record resource if your zone is in this same account) before the
# domain shows as "Verified" and can actually send.
resource "aws_sesv2_email_identity" "domain" {
  count          = var.manage_ses_domain_identity ? 1 : 0
  email_identity = var.ses_domain
}

output "ses_dkim_tokens" {
  value       = var.manage_ses_domain_identity ? aws_sesv2_email_identity.domain[0].dkim_signing_attributes : null
  description = "Add these as CNAME records at your DNS provider to verify DKIM for SES sending"
}
