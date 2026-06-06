using System.Threading.RateLimiting;
using System.Threading.Tasks;
using UserManagementAPI.Models;
using UserManagementAPI.Middleware;
using UserManagementAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Built-in rate limiting (partitioned by remote IP)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromSeconds(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    options.RejectionStatusCode = 429;
    options.OnRejected = (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "1";
        return ValueTask.CompletedTask;
    };
});

builder.Services.AddSingleton<RequestCountsStore>();

var app = builder.Build();

// catch unhandled exceptions and return consistent JSON errors
app.UseMiddleware<ExceptionHandlingMiddleware>();
// enable rate limiter early in the pipeline
app.UseRateLimiter();
// authenticate requests
app.UseMiddleware<AuthenticationMiddleware>();
// count requests
app.UseMiddleware<RequestCountingMiddleware>();
// log requests/responses last
app.UseMiddleware<RequestLoggingMiddleware>();

var users = new List<User>();
const int DefaultPageSize = 50;
const int MaxPageSize = 100;
var nextUserId = 1;

app.MapGet("/", () => Results.Ok(new { service = "User Management API" }));

app.MapGet("/users", (HttpRequest req) =>
{
    var page = int.TryParse(req.Query["page"], out var p) && p > 0 ? p : 1;
    var pageSize = int.TryParse(req.Query["pageSize"], out var s) && s > 0 ? Math.Min(s, MaxPageSize) : DefaultPageSize;
    var searchTerm = (req.Query["search"].ToString() ?? string.Empty).Trim();

    // snapshot to avoid concurrent modification while enumerating
    var usersSnapshot = users.ToArray().AsEnumerable();

    if (!string.IsNullOrEmpty(searchTerm))
    {
        usersSnapshot = usersSnapshot.Where(u =>
            u.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    var total = usersSnapshot.Count();
    var items = usersSnapshot
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(u => new { u.Id, u.Name, u.Email })
        .ToList();

    var result = new
    {
        total,
        page,
        pageSize,
        items
    };

    return Results.Ok(result);
});

app.MapGet("/users/{id:int}", (int id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapPost("/users", (UserDto request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest("Name and Email are required.");
    }

    var user = new User
    {
        Id = nextUserId++,
        Name = request.Name.Trim(),
        Email = request.Email.Trim()
    };

    users.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id:int}", (int id, UserDto request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest("Name and Email are required.");
    }

    var existingUser = users.FirstOrDefault(u => u.Id == id);
    if (existingUser is null)
    {
        return Results.NotFound();
    }

    existingUser.Name = request.Name.Trim();
    existingUser.Email = request.Email.Trim();
    return Results.NoContent();
});

app.MapDelete("/users/{id:int}", (int id) =>
{
    var existingUser = users.FirstOrDefault(u => u.Id == id);
    if (existingUser is null)
    {
        return Results.NotFound();
    }

    users.Remove(existingUser);
    return Results.NoContent();
});

app.MapGet("/counts", (RequestCountsStore store) => Results.Ok(store.GetCounts()));

app.MapGet("/throw", () => { throw new Exception("test"); });

app.Run();
