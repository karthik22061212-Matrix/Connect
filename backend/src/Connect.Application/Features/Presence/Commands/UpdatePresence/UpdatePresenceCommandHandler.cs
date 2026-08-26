using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Presence.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Presence.Commands.UpdatePresence;

public class UpdatePresenceCommandHandler : IRequestHandler<UpdatePresenceCommand, PresenceDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPresenceTracker _presenceTracker;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UpdatePresenceCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IPresenceTracker presenceTracker,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _presenceTracker = presenceTracker;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PresenceDto> Handle(UpdatePresenceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null || user.IsDeleted)
        {
            throw new NotFoundException("User not found.");
        }

        user.PresenceStatus = request.Status;
        user.UpdatedAt = _dateTimeProvider.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _presenceTracker.SetUserPresenceAsync(userId, request.Status);

        return new PresenceDto(user.Id, user.PresenceStatus, user.UpdatedAt);
    }
}
