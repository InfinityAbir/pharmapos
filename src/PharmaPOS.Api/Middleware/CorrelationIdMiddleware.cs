using System.Diagnostics;

namespace PharmaPOS.Api.Middleware;

/// <summary>
/// Ensures every request/response carries a correlation id. An inbound id is accepted only if it
/// is short and safe; otherwise a new one is generated. The id is attached to logs and returned in
/// the response so client errors can be traced server-side without exposing internals.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 128;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var inbound))
        {
            var candidate = inbound.ToString();
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= MaxLength)
            {
                return candidate;
            }
        }

        return Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
    }
}
