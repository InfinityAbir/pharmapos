using MediatR;
using PharmaPOS.Application.Common.Catalog;
using PharmaPOS.Domain.Exceptions;

namespace PharmaPOS.Application.Features.Catalog.GetMedicine;

public sealed class GetMedicineQueryHandler : IRequestHandler<GetMedicineQuery, MedicineDto>
{
    private readonly IMedicineRepository _medicines;

    public GetMedicineQueryHandler(IMedicineRepository medicines) => _medicines = medicines;

    public async Task<MedicineDto> Handle(
        GetMedicineQuery request,
        CancellationToken cancellationToken)
    {
        var medicine = await _medicines.FindByIdAsync(request.Id, cancellationToken);
        if (medicine is null)
        {
            throw new NotFoundException("medicine.not_found", $"Medicine '{request.Id}' was not found.");
        }

        return medicine;
    }
}