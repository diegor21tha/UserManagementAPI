using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace UserManagementAPI.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _validToken;

        public AuthenticationMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _validToken = config["Auth:ValidToken"] ?? string.Empty;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Expect Authorization: Bearer <token>
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) || string.IsNullOrWhiteSpace(authHeader))
            {
                await Reject(context);
                return;
            }

            var header = authHeader.ToString();
            const string bearerPrefix = "Bearer ";
            if (!header.StartsWith(bearerPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                await Reject(context);
                return;
            }

            var token = header.Substring(bearerPrefix.Length).Trim();
            if (string.IsNullOrEmpty(_validToken) || token != _validToken)
            {
                await Reject(context);
                return;
            }

            await _next(context);
        }

        private static async Task Reject(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var payload = JsonSerializer.Serialize(new { error = "Unauthorized" });
            await context.Response.WriteAsync(payload);
        }
    }
}
