using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PharmaPOS.Application.Features.Auth;
using PharmaPOS.Application.Features.Auth.Login;
using PharmaPOS.Application.Features.Auth.Refresh;
using PharmaPOS.Application.Features.Auth.Register;

namespace PharmaPOS.Api.Controllers;

/// <summary>
/// App-owned authentication backed by Keycloak. The SPA posts credentials here (Resource Owner
/// grant proxied server-side) instead of redirecting to Keycloak, and self-registers new
/// pharmacies. All calls stay server-to-Keycloak so the confidential client secret never
/// reaches the browser. Rate-limited strictly: these endpoints accept unauthenticated traffic.
/// Controllers translate HTTP only; all behaviour lives in the application layer.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("authentication")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender) => _sender = sender;

    /// <summary>Signs in with email + password. Returns access and refresh tokens.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Exchanges a still-valid refresh token for new tokens.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LoginResponse>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RefreshCommand(request.RefreshToken),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Registers a new pharmacy and signs its owner in. Creates a new tenant.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<LoginResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new RegisterCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.PharmacyName),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string PharmacyName);
