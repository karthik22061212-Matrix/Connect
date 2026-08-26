using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Account.Commands.SoftDeleteAccount;

public class SoftDeleteAccountCommandHandler : IRequestHandler<SoftDeleteAccountCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SoftDeleteAccountCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<bool> Handle(SoftDeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(currentUserId.Value, cancellationToken);
        if (user == null || user.IsDeleted)
        {
            throw new NotFoundException("User account not found or already deactivated.");
        }

        var now = _dateTimeProvider.UtcNow;
        user.IsDeleted = true;
        user.DeletedAt = now;
        user.ReactivationDeadline = now.AddDays(60);
        user.PresenceStatus = PresenceStatus.Offline;
        user.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
