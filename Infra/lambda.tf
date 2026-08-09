# Rebuilds whenever any source file changes.
resource "null_resource" "build" {

  provisioner "local-exec" {
    working_dir = "${path.module}/../API/ContactForm.API"
    command     = "dotnet publish -c Release -r linux-arm64 --self-contained false -o terraform/publish"
  }
}

data "archive_file" "lambda" {
  type        = "zip"
  source_dir  = "${path.module}/../API/ContactForm.API/terraform/publish"
  output_path = "${path.module}/../API/ContactForm.API/lambda.zip"
  depends_on  = [null_resource.build]
}

resource "aws_lambda_function" "this" {
  function_name = "${var.project_name}-fn"
  role          = aws_iam_role.lambda_exec.arn

  # Amazon.Lambda.AspNetCoreServer.Hosting self-bootstraps — the handler is
  # just the published assembly name, no ::Class::Method suffix needed.
  handler       = "ContactFormApi"
  runtime       = "dotnet8"
  architectures = ["arm64"]

  memory_size = var.lambda_memory_size
  timeout     = var.lambda_timeout

  filename         = data.archive_file.lambda.output_path
  source_code_hash = data.archive_file.lambda.output_base64sha256

  environment {
    variables = merge(
      {
        ASPNETCORE_ENVIRONMENT    = "Production"
        "Cors__AllowedOrigins__0" = var.allowed_origins[0]
        "Email__Provider"         = var.email_provider
        "Email__FromAddress"      = var.email_from
        "Email__ToAddress"        = var.email_to
        "Captcha__Enabled"        = tostring(var.captcha_enabled)
        "Captcha__VerifyUrl"      = var.captcha_verify_url
      },
      var.email_provider == "smtp" ? {
        "Email__Host"                       = var.email_host
        "Email__Port"                       = tostring(var.email_port)
        "Email__Username"                   = var.email_username
        "Email__PasswordSecretArn"          = aws_secretsmanager_secret.email_password[0].arn
        "Email__Password"                   = var.email_password
        "Email__AllowInvalidSslCertificate" = tostring(var.email_allow_invalid_ssl_certificate)
      } : {},
      var.captcha_enabled ? {
        "Captcha__SecretKeyArn"     = aws_secretsmanager_secret.captcha_secret[0].arn
        "Captcha__ExpectedHostname" = var.captcha_expected_hostname
      } : {}
    )
  }

  depends_on = [aws_iam_role_policy_attachment.basic_execution]
}

resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${var.project_name}-fn"
  retention_in_days = 30
}
