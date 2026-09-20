using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.UpdateDraft;

public sealed record UpdateDraftCommand(Guid Id, PurchaseInput Input) : IRequest<PurchaseDto>;