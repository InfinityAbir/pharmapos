namespace PharmaPOS.Application.Features.Platform.GetPlatformInfo;

public sealed record PlatformInfoDto(
    string Name,
    string Version,
    string Environment,
    DateTimeOffset ServerTimeUtc,
    string Status);
