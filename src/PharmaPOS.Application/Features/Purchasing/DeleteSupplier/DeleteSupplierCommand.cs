using MediatR;

namespace PharmaPOS.Application.Features.Purchasing.DeleteSupplier;

public sealed record DeleteSupplierCommand(Guid Id) : IRequest<Unit>;