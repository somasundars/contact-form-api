output "api_endpoint" {
  description = "Full contact form endpoint URL to call from your frontend"
  value       = "${aws_apigatewayv2_api.this.api_endpoint}/api/contact"
}

output "lambda_function_name" {
  value = aws_lambda_function.this.function_name
}
