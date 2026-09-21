using FluentValidation;
using MediatR;
using PharmaPOS.Application.Common.Identity;

namespace PharmaPOS.Application.Features.Auth.Login;

public sealed record LoginCommand(string Username, string Password) : IRequest<LoginResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Username).NotEmpty().MaximumLength(320);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(256);
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IKeycloakClient _keycloak;

    public LoginCommandHandler(IKeycloakClient keycloak) => _keycloak = keycloak;

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tokens = await _keycloak.TokenAsync(request.Username.Trim(), request.Password, cancellationToken);

        return new LoginResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds);
    }
}
