using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.SetSupplierStatus;

public sealed record SetSupplierStatusCommand(Guid Id, bool IsActive) : IRequest<SupplierDto>;