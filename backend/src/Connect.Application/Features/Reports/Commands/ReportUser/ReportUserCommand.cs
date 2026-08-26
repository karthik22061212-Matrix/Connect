using MediatR;

namespace Connect.Application.Features.Reports.Commands.ReportUser;

public record ReportUserCommand(
    Guid ReportedUserId,
    string Reason,
    string? Note
) : IRequest<Guid>;
