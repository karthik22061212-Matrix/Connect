using Connect.Application.Features.Calls.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Calls.Commands.FailCall;

public record FailCallCommand(
    Guid CallId,
    MissedReason Reason = MissedReason.ConnectionFailed
) : IRequest<FailCallResultDto>;
