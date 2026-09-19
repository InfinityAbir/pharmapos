using System.Reflection;
using MediatR;
using Microsoft.Extensions.Hosting;

namespace PharmaPOS.Application.Features.Platform.GetPlatformInfo;

public sealed class GetPlatformInfoQueryHandler : IRequestHandler<GetPlatformInfoQuery, PlatformInfoDto>
{
    private readonly IHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    public GetPlatformInfoQueryHandler(IHostEnvironment environment, TimeProvider timeProvider)
    {
        _environment = environment;
        _timeProvider = timeProvider;
    }

    public Task<PlatformInfoDto> Handle(GetPlatformInfoQuery request, CancellationToken cancellationToken)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";

        var dto = new PlatformInfoDto(
            Name: "PharmaPOS API",
            Version: version,
            Environment: _environment.EnvironmentName,
            ServerTimeUtc: _timeProvider.GetUtcNow(),
            Status: "ok");

        return Task.FromResult(dto);
    }
}
