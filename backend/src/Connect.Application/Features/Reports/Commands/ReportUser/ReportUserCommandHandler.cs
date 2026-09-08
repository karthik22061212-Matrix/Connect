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
    private readonly ICallRealtimeNotifier _callRealtimeNotifier;

    public ReportUserCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ICallRealtimeNotifier callRealtimeNotifier)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _callRealtimeNotifier = callRealtimeNotifier;
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

        var existingOpenReport = await _unitOfWork.Reports.FirstOrDefaultAsync(r =>
            r.ReporterUserId == currentUserId.Value &&
            r.ReportedUserId == request.ReportedUserId &&
            r.Status == ReportStatus.Open, cancellationToken);

        if (existingOpenReport != null)
        {
            return existingOpenReport.Id;
        }

        // Cascade 1: Sever Active Connection
        var minId = currentUserId.Value.CompareTo(request.ReportedUserId) < 0 ? currentUserId.Value : request.ReportedUserId;
        var maxId = currentUserId.Value.CompareTo(request.ReportedUserId) < 0 ? request.ReportedUserId : currentUserId.Value;

        var existingConnection = await _unitOfWork.Connections.FirstOrDefaultAsync(
            c => c.UserAId == minId && c.UserBId == maxId, cancellationToken);
        if (existingConnection != null)
        {
            _unitOfWork.Connections.Remove(existingConnection);
        }

        // Cascade 2: Cancel Pending Connect Requests (Both Directions)
        var pendingRequests = (await _unitOfWork.ConnectRequests.ListAsync(cancellationToken)) ?? Array.Empty<ConnectRequest>();
        var mutualPending = pendingRequests
            .Where(r => r.Status == ConnectRequestStatus.Pending &&
                       ((r.FromUserId == currentUserId.Value && r.ToUserId == request.ReportedUserId) ||
                        (r.FromUserId == request.ReportedUserId && r.ToUserId == currentUserId.Value)))
            .ToList();

        foreach (var req in mutualPending)
        {
            req.Status = ConnectRequestStatus.Declined;
            req.RespondedAt = _dateTimeProvider.UtcNow;
            req.UpdatedAt = _dateTimeProvider.UtcNow;
        }

        // Cascade 3: Mutual Search Exclusion (enforced automatically via Report row persistence)
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

        // Cascade 4: Terminate In-Flight Calls
        await _callRealtimeNotifier.TerminateActiveCallsBetweenUsersAsync(
            currentUserId.Value, request.ReportedUserId, "UserReported", cancellationToken);

        if (existingConnection != null)
        {
            await _callRealtimeNotifier.NotifyConnectionRemovedAsync(
                currentUserId.Value, request.ReportedUserId, cancellationToken);
        }

        return report.Id;
    }
}
