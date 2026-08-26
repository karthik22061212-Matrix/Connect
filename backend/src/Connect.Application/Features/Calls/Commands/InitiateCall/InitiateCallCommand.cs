using Connect.Application.Features.Calls.Models;
using MediatR;

namespace Connect.Application.Features.Calls.Commands.InitiateCall;

public record InitiateCallCommand(Guid CalleeId) : IRequest<CallResultDto>;
