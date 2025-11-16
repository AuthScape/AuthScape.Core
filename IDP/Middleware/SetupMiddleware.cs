using IDP.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace IDP.Middleware
{
    public class SetupMiddleware
    {
        private readonly RequestDelegate _next;

        public SetupMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISetupService setupService)
        {
            var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

            // Skip middleware for:
            // 1. Static files (css, js, images, etc.)
            // 2. The setup page itself
            // 3. Health check endpoints
            if (IsStaticFile(path) ||
                path.StartsWith("/setup") ||
                path.StartsWith("/health") ||
                path.StartsWith("/.well-known"))
            {
                await _next(context);
                return;
            }

            try
            {
                // Check if setup is required
                var setupRequired = await setupService.IsSetupRequiredAsync();

                if (setupRequired)
                {
                    // Redirect to setup page if not already there
                    if (!path.StartsWith("/setup"))
                    {
                        context.Response.Redirect("/Setup");
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // If setup service fails (e.g., database not accessible),
                // allow the request to continue to show proper error
                await _next(context);
                return;
            }

            await _next(context);
        }

        private bool IsStaticFile(string path)
        {
            var staticExtensions = new[]
            {
                ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".svg",
                ".ico", ".woff", ".woff2", ".ttf", ".eot", ".map",
                ".json", ".xml"
            };

            foreach (var ext in staticExtensions)
            {
                if (path.EndsWith(ext))
                {
                    return true;
                }
            }

            // Also check for lib folder
            if (path.StartsWith("/lib/") || path.StartsWith("/css/") || path.StartsWith("/js/") || path.StartsWith("/images/"))
            {
                return true;
            }

            return false;
        }
    }
}
