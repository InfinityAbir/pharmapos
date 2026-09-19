namespace PharmaPOS.Api.Middleware;

/// <summary>
/// Applies conservative security headers to every API response. The API only serves JSON, so a
/// strict <c>default-src 'none'</c> Content-Security-Policy is appropriate here; the Angular app
/// is served separately and defines its own policy at the reverse proxy.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-site";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'";

        // Do not advertise the server stack.
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        await _next(context);
    }
}
