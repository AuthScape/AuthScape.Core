# AuthScape Universal Email Library

A flexible, provider-agnostic email service for AuthScape that supports multiple email providers.

## Supported Email Providers

1. **SendGrid** - Fully implemented
2. **Mailgun** - Fully implemented
3. **MailKit (SMTP)** - Fully implemented (works with any SMTP server)
4. **SMTP2GO** - Fully implemented (uses MailKit)
5. **Azure Communication Email** - Stub implementation (requires Azure.Communication.Email NuGet package)

## Configuration

### appsettings.json Example

```json
{
  "Email": {
    "DefaultProvider": "SendGrid",
    "DefaultFromEmail": "noreply@yourcompany.com",
    "DefaultFromName": "Your Company",
    "Providers": {
      "SendGrid": {
        "Enabled": true,
        "ApiKey": "your-sendgrid-api-key-here"
      },
      "Mailgun": {
        "Enabled": false,
        "ApiKey": "your-mailgun-api-key",
        "Settings": {
          "Domain": "mg.yourcompany.com",
          "BaseUrl": "https://api.mailgun.net/v3"
        }
      },
      "MailKit": {
        "Enabled": false,
        "Settings": {
          "Host": "smtp.yourserver.com",
          "Port": "587",
          "Username": "your-username",
          "Password": "your-password",
          "UseSsl": "true"
        }
      },
      "SMTP2GO": {
        "Enabled": false,
        "Settings": {
          "Host": "mail.smtp2go.com",
          "Port": "2525",
          "Username": "your-username",
          "Password": "your-password",
          "UseSsl": "true"
        }
      },
      "AzureEmail": {
        "Enabled": false,
        "Settings": {
          "ConnectionString": "your-azure-communication-connection-string"
        }
      }
    }
  }
}
```

## Setup in Program.cs

```csharp
using AuthScape.Services.Mail.Configuration;

// Add email service with configuration from appsettings.json
builder.Services.AddEmailService(builder.Configuration);

// Or configure programmatically
builder.Services.AddEmailService(options =>
{
    options.DefaultProvider = "SendGrid";
    options.DefaultFromEmail = "noreply@yourcompany.com";
    options.DefaultFromName = "Your Company";

    options.Providers["SendGrid"] = new EmailProviderConfig
    {
        Enabled = true,
        ApiKey = "your-api-key"
    };
});
```

## Usage Examples

### Basic Email Sending

```csharp
using AuthScape.Services.Mail.Abstractions;
using AuthScape.Services.Mail.Core;

public class YourService
{
    private readonly IEmailService _emailService;

    public YourService(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task SendWelcomeEmail(string toEmail, string name)
    {
        var message = new EmailMessage
        {
            Subject = "Welcome to AuthScape!",
            FromEmail = "noreply@authscape.com",
            FromName = "AuthScape Team",
            HtmlContent = $"<h1>Welcome {name}!</h1><p>Thank you for joining AuthScape.</p>",
            TextContent = $"Welcome {name}! Thank you for joining AuthScape."
        };

        message.AddTo(toEmail, name);

        var response = await _emailService.SendEmailAsync(message);

        if (response.IsSuccess)
        {
            Console.WriteLine($"Email sent successfully! MessageId: {response.MessageId}");
        }
        else
        {
            Console.WriteLine($"Failed to send email: {response.ErrorMessage}");
        }
    }
}
```

### Using a Specific Provider

```csharp
// Send using a specific provider (overrides default)
var response = await _emailService.SendEmailAsync(message, "Mailgun");
```

### Advanced Email with Attachments

```csharp
var message = new EmailMessage
{
    Subject = "Invoice for Your Order",
    FromEmail = "billing@company.com",
    FromName = "Company Billing"
};

message.AddTo("customer@example.com", "Customer Name");
message.AddCc("accounting@company.com", "Accounting");

message.HtmlContent = "<h1>Invoice Attached</h1><p>Please find your invoice attached.</p>";
message.TextContent = "Invoice Attached - Please find your invoice attached.";

// Add attachment
byte[] pdfBytes = File.ReadAllBytes("invoice.pdf");
message.AddAttachment("invoice.pdf", pdfBytes, "application/pdf");

// Add custom headers
message.AddHeader("X-Invoice-ID", "INV-12345");

// Add tags for tracking
message.AddTag("invoices");
message.AddTag("billing");

var response = await _emailService.SendEmailAsync(message);
```

### Builder Pattern Usage

```csharp
var message = new EmailMessage()
    .AddTo("user1@example.com", "User One")
    .AddTo("user2@example.com", "User Two")
    .AddCc("manager@example.com", "Manager")
    .AddTag("newsletter")
    .AddHeader("X-Campaign-ID", "CAMP-2024-01");

message.Subject = "Monthly Newsletter";
message.HtmlContent = "<h1>This Month's Updates</h1>";
message.FromEmail = "newsletter@company.com";
message.FromName = "Company Newsletter";

await _emailService.SendEmailAsync(message);
```

## Migration from Existing SendGrid Service

The new email library is designed to work alongside the existing `ISendGridService`. You can migrate gradually:

1. Keep using `ISendGridService` for existing code
2. Use `IEmailService` for new features
3. Eventually migrate all email sending to `IEmailService`

### Migrating from ISendGridService

**Old Code (ISendGridService):**
```csharp
await _sendGridService.SendHtmlEmail(user, "Verification Email", htmlContent);
```

**New Code (IEmailService):**
```csharp
var message = new EmailMessage
{
    Subject = "Verification Email",
    FromEmail = "noreply@authscape.com",
    FromName = "AuthScape",
    HtmlContent = htmlContent,
    TextContent = StripHtml(htmlContent)
};
message.AddTo(user.Email, user.FirstName);

await _emailService.SendEmailAsync(message);
```

## Required NuGet Packages

### Core (Already Included)
- `SendGrid` (v9.29.3) - Already in AuthScape.Services
- `MailKit` (v4.8.0) - For SMTP support (already installed)

### Optional Provider Packages
Install only if you need these providers:

```bash
# Azure Communication Email (if using AzureEmail provider)
dotnet add package Azure.Communication.Email
```

## Testing

The library can be tested using the existing "Send Verification Email" functionality in the IDP Email management page.

## Architecture

The email library uses the **Strategy Pattern** combined with the **Factory Pattern**:

- **IEmailProvider**: Strategy interface for email providers
- **IEmailProviderFactory**: Factory for creating provider instances
- **IEmailService**: High-level service that uses the factory to get providers
- **EmailMessage**: Unified message format across all providers
- **EmailResponse**: Standardized response format

## Benefits

1. **Provider Agnostic**: Switch email providers without changing application code
2. **Unified API**: Same interface for all providers
3. **Easy Testing**: Mock IEmailService for unit tests
4. **Flexible Configuration**: Configure via appsettings.json or code
5. **Backward Compatible**: Works alongside existing SendGrid service
6. **Extensible**: Easy to add new providers
7. **Dependency Injection**: Full DI support
8. **Logging**: Built-in logging for debugging
9. **Error Handling**: Consistent error responses across providers

## Roadmap

- [ ] Complete Azure Communication Email provider implementation
- [ ] Add batch email sending support
- [ ] Add template support for all providers
- [ ] Add email queue/retry mechanism
- [ ] Add webhook support for delivery tracking
- [ ] Add email validation helpers
- [ ] Add unit tests
- [ ] Add integration tests
