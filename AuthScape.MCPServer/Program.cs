using AuthScape.MCPServer.Middleware;
using Microsoft.EntityFrameworkCore;
using Services.Context;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// Add database context - connects to same database as IDP
builder.Services.AddDbContext<DatabaseContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration.GetSection("AppSettings:DatabaseContext").Value,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    options.UseOpenIddict();
});

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
