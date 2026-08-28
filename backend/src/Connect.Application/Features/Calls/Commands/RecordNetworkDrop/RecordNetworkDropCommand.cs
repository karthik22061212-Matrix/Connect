using Connect.Application.Features.Calls.Models;
using MediatR;

namespace Connect.Application.Features.Calls.Commands.RecordNetworkDrop;

public record RecordNetworkDropCommand(Guid CallId) : IRequest<RecordNetworkDropResultDto>;
