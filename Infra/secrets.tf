# Only needed for the SMTP email provider — SES auth is via IAM, no secret required.
resource "aws_secretsmanager_secret" "email_password" {
  count = var.email_provider == "smtp" ? 1 : 0
  name  = "${var.project_name}/email-password"
}

resource "aws_secretsmanager_secret_version" "email_password" {
  depends_on    = [aws_secretsmanager_secret.email_password]
  count         = var.email_provider == "smtp" ? 1 : 0
  secret_id     = aws_secretsmanager_secret.email_password[0].id
  secret_string = var.email_password
}

resource "aws_secretsmanager_secret" "captcha_secret" {
  count = var.captcha_enabled ? 1 : 0
  name  = "${var.project_name}/captcha-secret"
}

resource "aws_secretsmanager_secret_version" "captcha_secret" {
  depends_on    = [aws_secretsmanager_secret.captcha_secret]
  count         = var.captcha_enabled ? 1 : 0
  secret_id     = aws_secretsmanager_secret.captcha_secret[0].id
  secret_string = var.captcha_secret_key
}
