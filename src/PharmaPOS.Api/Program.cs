using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Api.Middleware;
using PharmaPOS.Application;
using PharmaPOS.Application.Common.Identity;
using PharmaPOS.Infrastructure;
using PharmaPOS.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Logging: structured, correlation-enriched.
// ---------------------------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---------------------------------------------------------------------------------------------
// Application + infrastructure composition.
// ---------------------------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------------------------
// Configuration, validated at startup (fail fast on bad settings).
// ---------------------------------------------------------------------------------------------
builder.Services
    .AddOptions<KeycloakOptions>()
    .Bind(builder.Configuration.GetSection(KeycloakOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ---------------------------------------------------------------------------------------------
// Authentication: Keycloak-issued JWT access tokens only.
// The API validates tokens; login/refresh/registration go through AuthController, which proxies
// Keycloak server-side so the confidential client secret never reaches the browser.
// Authorization reads Keycloak realm roles (mapped to role claims below); the token proves who
// the caller is AND what they may do, while tenant_id still selects the tenant context.
// ---------------------------------------------------------------------------------------------
var keycloak = builder.Configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>()
    ?? new KeycloakOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloak.Authority;
        options.Audience = keycloak.Audience;
        options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "preferred_username",
        };

        // Split-horizon: the discovery document uses the internal Keycloak address, but tokens
        // carry the public issuer. Accept the configured issuers explicitly.
        if (keycloak.ValidIssuers.Length > 0)
        {
            options.TokenValidationParameters.ValidIssuers = keycloak.ValidIssuers;
        }

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // Security logging: never log the token itself.
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Security.Authentication")
                    .LogWarning(
                        context.Exception,
                        "Token validation failed for {Method} {Path}",
                        context.HttpContext.Request.Method,
                        context.HttpContext.Request.Path);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                // Keycloak realm roles arrive either in the standard realm_access.roles blob or in
                // this realm's "roles" array claim (see the realm-roles protocol mapper). Copy both
                // shapes across as role claims so role-based authorization sees them.
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    KeycloakRoleMapping.MapRoleArrayClaim(identity, "realm_access", "roles");
                    KeycloakRoleMapping.MapRoleArrayClaim(identity, null, "roles");
                }

                return Task.CompletedTask;
            },
        };
    });

// ---------------------------------------------------------------------------------------------
// Authorization: permission-based, server-evaluated.
// ---------------------------------------------------------------------------------------------
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
// Scoped: the handler resolves permissions through the request-scoped membership graph.
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ---------------------------------------------------------------------------------------------
// MVC, OpenAPI/Swagger.
// ---------------------------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---------------------------------------------------------------------------------------------
// Global exception handling (RFC 7807/9457) — never leaks internals.
// ---------------------------------------------------------------------------------------------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
    };
});

// ---------------------------------------------------------------------------------------------
// Health checks: separate liveness from readiness (readiness includes PostgreSQL).
// ---------------------------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Database connection string 'Database' is not configured.");

builder.Services
    .AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

// ---------------------------------------------------------------------------------------------
// CORS: explicit origin allow-list. Never AllowAnyOrigin (see ADR-0010).
// ---------------------------------------------------------------------------------------------
const string SpaCorsPolicy = "spa";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy(SpaCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
        .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
        .WithExposedHeaders("X-Correlation-Id")
        .AllowCredentials()));
}

// ---------------------------------------------------------------------------------------------
// Rate limiting: a broad per-caller budget plus a strict policy for authentication endpoints.
// ---------------------------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var key = httpContext.User.Identity?.IsAuthenticated == true
            ? $"user:{httpContext.User.FindFirst("sub")?.Value}"
            : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });

    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
        }, cancellationToken);
    };
});

var app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Request pipeline.
//
// When hosted behind the production reverse proxy (see docker-compose.prod.yml), TLS is
// terminated at the proxy and the original scheme/client IP arrive as X-Forwarded-*
// headers. Honor them first so HTTPS redirection, HSTS, IP-based rate limiting, audit
// IPs and request logs observe the real client values. The proxy strips any inbound
// X-Forwarded-* headers from external clients, and the API container only trusts traffic
// from the internal compose network.
// ---------------------------------------------------------------------------------------------
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};

// The API container has no fixed proxy address (compose-assigned IPs change per deploy),
// so the loopback-only default would silently ignore the proxy headers. Trust headers from
// the internal compose network instead: the proxy strips client-supplied X-Forwarded-*
// headers at the edge, and the API port is not published publicly in production.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);
app.UseSerilogRequestLogging();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (allowedOrigins.Length > 0)
{
    app.UseCors(SpaCorsPolicy);
}

app.UseAuthentication();
// Tenant resolution runs after authentication so it can read the validated principal, and before
// authorization so tenant-scoped policies see the resolved context.
app.UseMultiTenant();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Liveness: process is up. Readiness: dependencies (PostgreSQL) reachable.
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

await app.Services.InitializeDatabaseAsync();

await app.RunAsync();

/// <summary>Exposed so integration tests can host the API via <c>WebApplicationFactory</c>.</summary>
public partial class Program;

/// <summary>
/// Copies realm role names into role claims. <c>containerClaim</c> names a JSON-blob claim
/// holding a <c>roles</c> array (standard <c>realm_access</c>); when null, the named claim itself
/// is read (repeated claims, or a single JSON-array value).
/// </summary>
internal static class KeycloakRoleMapping
{
    internal static void MapRoleArrayClaim(ClaimsIdentity identity, string? containerClaim, string claimName)
    {
        foreach (var claim in identity.FindAll(containerClaim ?? claimName))
        {
            if (containerClaim is not null && claim.Type != containerClaim)
            {
                continue;
            }

            foreach (var roleName in ReadRoleNames(claim.Value))
            {
                if (!identity.HasClaim(ClaimTypes.Role, roleName))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }
        }
    }

    private static IEnumerable<string> ReadRoleNames(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('['))
        {
            List<string>? names = null;
            try
            {
                using var json = JsonDocument.Parse(trimmed);
                if (json.RootElement.ValueKind == JsonValueKind.Array)
                {
                    names = [];
                    foreach (var role in json.RootElement.EnumerateArray())
                    {
                        var name = role.ValueKind == JsonValueKind.String
                            ? role.GetString()
                            : role.GetProperty("name").GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return [];
            }

            return names ?? [];
        }

        return string.IsNullOrEmpty(trimmed) ? [] : [trimmed];
    }
}
