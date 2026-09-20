using MediatR;
using PharmaPOS.Application.Common.Purchasing;

namespace PharmaPOS.Application.Features.Purchasing.CreateDraft;

public sealed record CreateDraftCommand(PurchaseInput Input) : IRequest<PurchaseDto>;