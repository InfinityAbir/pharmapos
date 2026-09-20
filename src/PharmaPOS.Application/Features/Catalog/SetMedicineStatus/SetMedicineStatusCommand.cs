using MediatR;

namespace PharmaPOS.Application.Features.Catalog.SetMedicineStatus;

public sealed record SetMedicineStatusCommand(Guid Id, bool IsActive) : IRequest<MedicineDto>;