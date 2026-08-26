using Connect.Application.Common.Interfaces;
using Connect.Application.Common.Models;
using Connect.Application.Features.Calls.Models;
using MediatR;

namespace Connect.Application.Features.Calls.Queries.GetCallHistory;

public class GetCallHistoryQueryHandler : IRequestHandler<GetCallHistoryQuery, PaginatedList<CallHistoryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetCallHistoryQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PaginatedList<CallHistoryDto>> Handle(GetCallHistoryQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var cutoff = _dateTimeProvider.UtcNow.AddDays(-90);

        var allCalls = await _unitOfWork.Calls.ListAsync(cancellationToken);
        var filteredCalls = allCalls
            .Where(c => (c.CallerId == currentUserId.Value || c.CalleeId == currentUserId.Value)
                        && c.StartedAt >= cutoff)
            .OrderByDescending(c => c.StartedAt)
            .ToList();

        var totalCount = filteredCalls.Count;
        var pagedCalls = filteredCalls
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var allUsers = await _unitOfWork.Users.ListAsync(cancellationToken);
        var userDict = allUsers.ToDictionary(u => u.Id, u => u.UserId);

        var dtos = pagedCalls.Select(c => new CallHistoryDto(
            c.Id,
            c.CallerId,
            userDict.GetValueOrDefault(c.CallerId, string.Empty),
            c.CalleeId,
            userDict.GetValueOrDefault(c.CalleeId, string.Empty),
            c.CallerId == currentUserId.Value,
            c.Status,
            c.MissedReason,
            c.StartedAt,
            c.AnsweredAt,
            c.EndedAt,
            c.DurationSeconds
        )).ToList();

        return PaginatedList<CallHistoryDto>.Create(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
