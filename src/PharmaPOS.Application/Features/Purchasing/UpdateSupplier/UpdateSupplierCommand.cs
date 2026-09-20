using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.UpdateSupplier;

public sealed record UpdateSupplierCommand(Guid Id, SupplierInput Input) : IRequest<SupplierDto>;