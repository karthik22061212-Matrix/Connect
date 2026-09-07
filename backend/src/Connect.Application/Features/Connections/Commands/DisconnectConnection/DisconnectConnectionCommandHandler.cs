using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.Connections.Commands.DisconnectConnection;

public class DisconnectConnectionCommandHandler : IRequestHandler<DisconnectConnectionCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DisconnectConnectionCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DisconnectConnectionCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        if (currentUserId == request.TargetUserId)
        {
            throw new ConflictException("Cannot disconnect from yourself.");
        }

        var userAId = currentUserId.CompareTo(request.TargetUserId) < 0 ? currentUserId : request.TargetUserId;
        var userBId = currentUserId.CompareTo(request.TargetUserId) < 0 ? request.TargetUserId : currentUserId;

        var connection = await _unitOfWork.Connections
            .FirstOrDefaultAsync(c => c.UserAId == userAId && c.UserBId == userBId, cancellationToken);

        if (connection == null)
        {
            throw new NotFoundException("Connection not found.");
        }

        _unitOfWork.Connections.Remove(connection);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
