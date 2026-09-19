using MediatR;

namespace PharmaPOS.Application.Features.Platform.GetPlatformInfo;

/// <summary>Returns basic, non-sensitive information about the running API (used by the shell/health UI).</summary>
public sealed record GetPlatformInfoQuery : IRequest<PlatformInfoDto>;
