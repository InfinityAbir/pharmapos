using MediatR;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.UpdateMedicine;

public sealed record UpdateMedicineCommand(Guid Id, MedicineInput Input) : IRequest<MedicineDto>;