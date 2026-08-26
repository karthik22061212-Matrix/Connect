using Connect.Application.Features.ConnectRequests.Models;
using MediatR;

namespace Connect.Application.Features.ConnectRequests.Queries.GetPendingRequests;

public record GetPendingRequestsQuery : IRequest<IEnumerable<PendingConnectRequestDto>>;
