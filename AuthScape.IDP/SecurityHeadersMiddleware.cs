using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace AuthScape.IDP
{
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _csp;

        public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
        {
            _next = next;

            var devConnect = env.IsDevelopment() ? " http://localhost:* ws://localhost:* wss://localhost:*" : string.Empty;

            _csp =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' https://js.stripe.com; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' data:; " +
                $"connect-src 'self'{devConnect}; " +
                "frame-src 'self' https://js.stripe.com; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'";
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
