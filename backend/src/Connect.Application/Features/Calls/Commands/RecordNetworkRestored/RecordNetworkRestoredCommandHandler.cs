using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Models;
using Connect.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.Calls.Commands.RecordNetworkRestored;

public class RecordNetworkRestoredCommandHandler : IRequestHandler<RecordNetworkRestoredCommand, RecordNetworkRestoredResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RecordNetworkRestoredCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<RecordNetworkRestoredResultDto> Handle(RecordNetworkRestoredCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var call = await _unitOfWork.Calls.GetByIdAsync(request.CallId, cancellationToken);
        if (call == null)
        {
            throw new NotFoundException("Call not found.");
        }

        if (call.CallerId != currentUserId && call.CalleeId != currentUserId)
        {
            throw new ForbiddenAccessException("You are not a participant in this call.");
        }

        if (call.TimeoutType == CallTimeoutType.Reconnect)
        {
            call.TimeoutDeadline = null;
            call.TimeoutType = null;
            call.UpdatedAt = _dateTimeProvider.UtcNow;
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entry = ex.Entries.FirstOrDefault();
                if (entry != null)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }

        var otherUserId = call.CallerId == currentUserId ? call.CalleeId : call.CallerId;

        return new RecordNetworkRestoredResultDto(call.Id, call.CallerId, call.CalleeId, otherUserId);
    }
}
