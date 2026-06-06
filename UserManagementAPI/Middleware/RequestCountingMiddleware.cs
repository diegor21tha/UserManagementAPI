using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UserManagementAPI.Services;
using Microsoft.AspNetCore.Routing;

namespace UserManagementAPI.Middleware
{
    public class RequestCountingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RequestCountsStore _store;

        public RequestCountingMiddleware(RequestDelegate next, RequestCountsStore store)
        {
            _next = next;
            _store = store;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Execute the pipeline first so endpoint information is available
            await _next(context);

            string key;
            var endpoint = context.GetEndpoint();
            if (endpoint is RouteEndpoint routeEndpoint)
            {
                key = routeEndpoint.RoutePattern?.RawText ?? context.Request.Path.ToString();
            }
            else
            {
                key = context.Request.Path.ToString();
            }

            _store.Increment(key);
        }
    }
}
