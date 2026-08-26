using Connect.Application.Features.Connections.Models;
using MediatR;

namespace Connect.Application.Features.Connections.Queries.GetConnections;

public record GetConnectionsQuery : IRequest<IEnumerable<ConnectionDto>>;
