using Connect.Application.Features.Calls.Models;
using MediatR;

namespace Connect.Application.Features.Calls.Commands.RecordNetworkRestored;

public record RecordNetworkRestoredCommand(Guid CallId) : IRequest<RecordNetworkRestoredResultDto>;
