using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Reports.Commands.ReportUser;

public class ReportUserCommandHandler : IRequestHandler<ReportUserCommand, Guid>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ReportUserCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Guid> Handle(ReportUserCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        if (currentUserId.Value == request.ReportedUserId)
        {
            throw new ConflictException("Cannot report yourself.");
        }

        var reportedUser = await _unitOfWork.Users.GetByIdAsync(request.ReportedUserId, cancellationToken);
        if (reportedUser == null || reportedUser.IsDeleted)
        {
            throw new NotFoundException("Reported user not found.");
        }

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterUserId = currentUserId.Value,
            ReportedUserId = request.ReportedUserId,
            Reason = request.Reason,
            Note = request.Note,
            Status = ReportStatus.Open,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow
        };

        _unitOfWork.Reports.Add(report);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return report.Id;
    }
}
