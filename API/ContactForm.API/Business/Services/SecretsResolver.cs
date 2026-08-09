using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace ContactForm.API.Business.Services;

/// <summary>
/// Lambda environment variables are visible to anyone with lambda:GetFunctionConfiguration,
/// so we never put raw secrets there. Terraform instead injects the Secrets Manager ARN
/// (e.g. "Captcha:SecretKeyArn"), and this resolves the real value once at cold start,
/// overriding the plain config key ("Captcha:SecretKey") in memory for the app's lifetime.
/// No-op locally, where the plain keys are just set directly in appsettings/user-secrets.
/// </summary>
public static class SecretsResolver
{
    public static async Task ResolveAsync(WebApplicationBuilder builder)
    {
        var overrides = new Dictionary<string, string?>();
        using var client = new AmazonSecretsManagerClient();

        await TryResolve(client, builder.Configuration, "Email:PasswordSecretArn", "Email:Password", overrides);
        await TryResolve(client, builder.Configuration, "Captcha:SecretKeyArn", "Captcha:SecretKey", overrides);

        if (overrides.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(overrides);
        }
    }

    private static async Task TryResolve(
        AmazonSecretsManagerClient client,
        IConfiguration config,
        string arnKey,
        string targetKey,
        Dictionary<string, string?> overrides)
    {
        var arn = config[arnKey];
        if (string.IsNullOrWhiteSpace(arn))
        {
            return;
        }

        var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = arn });
        overrides[targetKey] = response.SecretString;
    }
}