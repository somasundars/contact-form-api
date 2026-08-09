data "aws_iam_policy_document" "assume_role" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "lambda_exec" {
  name               = "${var.project_name}-lambda-role"
  assume_role_policy = data.aws_iam_policy_document.assume_role.json
}

resource "aws_iam_role_policy_attachment" "basic_execution" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# Read-only access to just the two secrets this function needs — never a
# broad secretsmanager:* grant.
data "aws_iam_policy_document" "secrets_access" {
  statement {
    actions   = ["secretsmanager:GetSecretValue"]
    resources = local.secret_arns
  }
}

resource "aws_iam_role_policy" "secrets_access" {
  depends_on = [aws_iam_role.lambda_exec]
  name       = "${var.project_name}-secrets-access"
  role       = aws_iam_role.lambda_exec.id
  policy     = data.aws_iam_policy_document.secrets_access.json
}

# SES send permission, scoped to the configured From address only — only
# created when email_provider = "ses".
data "aws_iam_policy_document" "ses_send" {
  count = var.email_provider == "ses" ? 1 : 0

  statement {
    actions   = ["ses:SendEmail", "ses:SendRawEmail"]
    resources = ["*"]
    condition {
      test     = "StringEquals"
      variable = "ses:FromAddress"
      values   = [var.email_from]
    }
  }
}

resource "aws_iam_role_policy" "ses_send" {
  count  = var.email_provider == "ses" ? 1 : 0
  name   = "${var.project_name}-ses-send"
  role   = aws_iam_role.lambda_exec.id
  policy = data.aws_iam_policy_document.ses_send[0].json
}

locals {
  secret_arns = compact([
    var.email_provider == "smtp" ? aws_secretsmanager_secret.email_password[0].arn : "",
    var.captcha_enabled ? aws_secretsmanager_secret.captcha_secret[0].arn : ""
  ])
}
