using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace AuthScape.Controllers
{
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _csp;

        public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;

            if (env.IsDevelopment())
            {
                // Dev: Scalar API explorer + Browser Link + aspnetcore-browser-refresh all need
                // self scripts/styles, inline execution, and localhost WebSocket/HTTP connections.
                _csp =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' http://localhost:* https://localhost:*; " +
                    "style-src 'self' 'unsafe-inline'; " +
                    "img-src 'self' data: https:; " +
                    "font-src 'self' data:; " +
                    "connect-src 'self' http://localhost:* https://localhost:* ws://localhost:* wss://localhost:*; " +
                    "frame-ancestors 'none'; " +
                    "base-uri 'self'";
            }
            else
            {
                // Prod: the API returns JSON only — lock everything down.
                _csp =
                    "default-src 'none'; " +
                    "frame-ancestors 'none'; " +
                    "base-uri 'none'";
            }
        }

        public Task Invoke(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var h = context.Response.Headers;

                h.Remove("Server");
                h.Remove("X-Powered-By");

                h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                h["X-Content-Type-Options"]    = "nosniff";
                h["X-Frame-Options"]           = "DENY";
                h["Referrer-Policy"]           = "strict-origin-when-cross-origin";
                h["Content-Security-Policy"]   = _csp;

                return Task.CompletedTask;
            });

            return _next(context);
        }
    }
}
