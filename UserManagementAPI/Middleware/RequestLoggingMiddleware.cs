using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace UserManagementAPI.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;

            // Read request body (allow rewind)
            request.EnableBuffering();
            string requestBody = string.Empty;
            if (request.ContentLength > 0)
            {
                request.Body.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Seek(0, SeekOrigin.Begin);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"HTTP {request.Method} {request.Path}{request.QueryString}");
            foreach (var h in request.Headers)
            {
                sb.AppendLine($"ReqHeader: {h.Key}: {h.Value}");
            }
            if (!string.IsNullOrEmpty(requestBody))
            {
                sb.AppendLine($"Request Body: {requestBody}");
            }

            // Capture response body
            var originalResponseBody = context.Response.Body;
            await using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            var sw = Stopwatch.StartNew();
            await _next(context);
            sw.Stop();

            // Read response
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            string responseBody = string.Empty;
            using (var reader = new StreamReader(responseBodyStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                responseBody = await reader.ReadToEndAsync();
            }
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;

            sb.AppendLine($"Response {context.Response.StatusCode} in {sw.ElapsedMilliseconds}ms");
            foreach (var h in context.Response.Headers)
            {
                sb.AppendLine($"ResHeader: {h.Key}: {h.Value}");
            }
            if (!string.IsNullOrEmpty(responseBody))
            {
                sb.AppendLine($"Response Body: {responseBody}");
            }

            _logger.LogInformation(sb.ToString());
        }
    }
}
