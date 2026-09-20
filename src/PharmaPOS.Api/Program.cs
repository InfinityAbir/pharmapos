using System.Threading.RateLimiting;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PharmaPOS.Api.Authentication;
using PharmaPOS.Api.Configuration;
using PharmaPOS.Api.Middleware;
using PharmaPOS.Application;
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
// The API validates tokens; it never issues, refreshes or rotates tokens (Keycloak owns the
// token lifecycle: short-lived access tokens + rotating refresh tokens with reuse detection).
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
            RoleClaimType = "roles",
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
// ---------------------------------------------------------------------------------------------
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
