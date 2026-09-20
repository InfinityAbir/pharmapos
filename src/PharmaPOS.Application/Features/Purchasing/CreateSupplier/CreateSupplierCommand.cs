using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.CreateSupplier;

public sealed record CreateSupplierCommand(SupplierInput Input) : IRequest<SupplierDto>;