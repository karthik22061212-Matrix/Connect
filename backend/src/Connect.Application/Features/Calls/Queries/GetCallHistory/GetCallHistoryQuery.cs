using Connect.Application.Common.Models;
using Connect.Application.Features.Calls.Models;
using MediatR;

namespace Connect.Application.Features.Calls.Queries.GetCallHistory;

public record GetCallHistoryQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<PaginatedList<CallHistoryDto>>;
