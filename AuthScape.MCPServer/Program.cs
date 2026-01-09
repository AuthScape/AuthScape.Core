using AuthScape.MCPServer.Middleware;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using Services.Database;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// Get AppSettings configuration for database provider
var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

// If connection string not in AppSettings, try ConnectionStrings section
if (string.IsNullOrEmpty(appSettings.DatabaseContext))
{
    appSettings.DatabaseContext = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration.GetSection("AppSettings:DatabaseContext").Value
        ?? "";
}

// Add database context - connects to same database as IDP
// Supports: SqlServer, PostgreSQL, MySQL, SQLite (configured via AppSettings:DatabaseProvider)
builder.Services.AddAuthScapeDatabase(
    appSettings,
    enableSensitiveDataLogging: builder.Environment.IsDevelopment(),
    useOpenIddict: true,
    lifetime: ServiceLifetime.Scoped);

// Add OpenIddict for client management (not for token validation - this is the OAuth server side)
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<DatabaseContext>();
    });

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Configure CORS for Claude Desktop
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

// MCP OAuth middleware - handles /.well-known/oauth-authorization-server and client registration
app.UseMcpOAuth();

app.UseRouting();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
