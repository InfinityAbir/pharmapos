using MediatR;
using PharmaPOS.Application.Common.Catalog;

namespace PharmaPOS.Application.Features.Catalog.CreateMedicine;

public sealed record CreateMedicineCommand(MedicineInput Input) : IRequest<MedicineDto>;