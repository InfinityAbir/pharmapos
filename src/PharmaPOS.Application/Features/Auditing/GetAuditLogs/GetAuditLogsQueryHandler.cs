using FluentValidation;
using MediatR;

namespace PharmaPOS.Application.Features.Auditing.GetAuditLogs;

public sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(query => query.Take)
            .InclusiveBetween(1, 200);
    }
}

public sealed class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    private readonly IAuditLogReader _reader;

    public GetAuditLogsQueryHandler(IAuditLogReader reader) => _reader = reader;

    public Task<IReadOnlyList<AuditLogDto>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
        => _reader.GetRecentAsync(request.Take, cancellationToken);
}
