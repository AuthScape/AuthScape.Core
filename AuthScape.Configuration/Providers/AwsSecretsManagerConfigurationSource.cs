using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using AuthScape.Configuration.Options;

namespace AuthScape.Configuration.Providers;

/// <summary>
/// Configuration source for AWS Secrets Manager.
/// </summary>
public class AwsSecretsManagerConfigurationSource : IConfigurationSource
{
    private readonly AwsSecretsManagerOptions _options;

    public AwsSecretsManagerConfigurationSource(AwsSecretsManagerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new AwsSecretsManagerConfigurationProvider(_options);
    }
}

/// <summary>
/// Configuration provider that loads secrets from AWS Secrets Manager.
/// Secrets should be stored as JSON with keys using double underscore for nesting.
/// Example: { "AppSettings__Stripe__SecretKey": "sk_live_xxx" }
/// </summary>
public class AwsSecretsManagerConfigurationProvider : ConfigurationProvider
{
    private readonly AwsSecretsManagerOptions _options;

    public AwsSecretsManagerConfigurationProvider(AwsSecretsManagerOptions options)
    {
        _options = options;
    }

    public override void Load()
    {
        if (string.IsNullOrEmpty(_options.SecretId))
        {
            return;
        }

        try
        {
            LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log but don't fail - allow other providers to supply values
            Console.WriteLine($"Warning: Failed to load from AWS Secrets Manager: {ex.Message}");
        }
    }

    private async Task LoadAsync()
    {
        using var client = CreateClient();

        var request = new GetSecretValueRequest
        {
            SecretId = _options.SecretId
        };

        var response = await client.GetSecretValueAsync(request);

        if (string.IsNullOrEmpty(response.SecretString))
        {
            return;
        }

        // Parse the secret JSON
        using var document = JsonDocument.Parse(response.SecretString);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            // Convert AWS key format (double underscore) to .NET configuration format (colon)
            var configKey = property.Name.Replace("__", ConfigurationPath.KeyDelimiter);

            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText()
            };

            if (value != null)
            {
                Data[configKey] = value;
            }
        }
    }

    private IAmazonSecretsManager CreateClient()
    {
        var region = RegionEndpoint.GetBySystemName(_options.Region ?? "us-east-1");

        if (_options.UseDefaultCredentials)
        {
            // Uses AWS credentials from environment, IAM role, etc.
            return new AmazonSecretsManagerClient(region);
        }

        if (!string.IsNullOrEmpty(_options.AccessKeyId) && !string.IsNullOrEmpty(_options.SecretAccessKey))
        {
            return new AmazonSecretsManagerClient(
                _options.AccessKeyId,
                _options.SecretAccessKey,
                region);
        }

        // Fall back to default credentials
        return new AmazonSecretsManagerClient(region);
    }
}
