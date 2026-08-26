using Connect.Application.Features.Calls.Models;
using MediatR;

namespace Connect.Application.Features.Calls.Commands.EndCall;

public record EndCallCommand(Guid CallId) : IRequest<EndCallResultDto>;
