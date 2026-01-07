using AuthScape.MCP;

// MCP Server Entry Point - Uses stdio transport for Claude Desktop compatibility
//
// Configuration can be provided via:
//   1. Command-line arguments: AuthScape.MCP.exe --url https://api.example.com --token your-token
//   2. Environment variables: AUTHSCAPE_API_URL and AUTHSCAPE_ACCESS_TOKEN
//
// Example Claude Desktop config (claude_desktop_config.json):
// {
//   "mcpServers": {
//     "authscape-cms": {
//       "command": "C:/path/to/AuthScape.MCP.exe",
//       "args": ["--url", "https://api.example.com", "--token", "your-token"]
//     }
//   }
// }

string? apiUrl = null;
string? accessToken = null;

// Parse command-line arguments
for (int i = 0; i < args.Length; i++)
{
    if ((args[i] == "--url" || args[i] == "-u") && i + 1 < args.Length)
    {
        apiUrl = args[++i];
    }
    else if ((args[i] == "--token" || args[i] == "-t") && i + 1 < args.Length)
    {
        accessToken = args[++i];
    }
    else if (args[i] == "--help" || args[i] == "-h")
    {
        Console.WriteLine(@"
AuthScape MCP Server - Enables Claude to manage your CMS pages

USAGE:
    AuthScape.MCP.exe [OPTIONS]

OPTIONS:
    -u, --url <URL>       AuthScape API URL (e.g., https://api.example.com)
    -t, --token <TOKEN>   Access token for authentication
    -h, --help            Show this help message

ENVIRONMENT VARIABLES:
    AUTHSCAPE_API_URL         API URL (overridden by --url)
    AUTHSCAPE_ACCESS_TOKEN    Access token (overridden by --token)

EXAMPLES:
    AuthScape.MCP.exe --url https://api.mysite.com --token abc123

    # Or use environment variables:
    set AUTHSCAPE_API_URL=https://api.mysite.com
    set AUTHSCAPE_ACCESS_TOKEN=abc123
    AuthScape.MCP.exe

CLAUDE DESKTOP CONFIG:
    Add to %APPDATA%\Claude\claude_desktop_config.json:

    {
      ""mcpServers"": {
        ""authscape-cms"": {
          ""command"": ""C:/path/to/AuthScape.MCP.exe"",
          ""args"": [""--url"", ""https://api.mysite.com"", ""--token"", ""your-token""]
        }
      }
    }
");
        return;
    }
}

// Fall back to environment variables if not provided via args
apiUrl ??= Environment.GetEnvironmentVariable("AUTHSCAPE_API_URL");
accessToken ??= Environment.GetEnvironmentVariable("AUTHSCAPE_ACCESS_TOKEN");

// Validate configuration
if (string.IsNullOrEmpty(apiUrl))
{
    Console.Error.WriteLine("Error: API URL is required. Use --url or set AUTHSCAPE_API_URL environment variable.");
    Console.Error.WriteLine("Run with --help for usage information.");
    Environment.Exit(1);
}

if (string.IsNullOrEmpty(accessToken))
{
    Console.Error.WriteLine("Error: Access token is required. Use --token or set AUTHSCAPE_ACCESS_TOKEN environment variable.");
    Console.Error.WriteLine("Run with --help for usage information.");
    Environment.Exit(1);
}

// Run the MCP server
var server = new McpServer(apiUrl, accessToken);
await server.RunAsync();
