using MediatR;

namespace PharmaPOS.Application.Features.Catalog.GetMedicine;

public sealed record GetMedicineQuery(Guid Id) : IRequest<MedicineDto>;