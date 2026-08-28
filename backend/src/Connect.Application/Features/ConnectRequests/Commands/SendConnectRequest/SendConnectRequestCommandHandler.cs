using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Models;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.ConnectRequests.Commands.SendConnectRequest;

public class SendConnectRequestCommandHandler : IRequestHandler<SendConnectRequestCommand, ConnectRequestDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SendConnectRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ConnectRequestDto> Handle(SendConnectRequestCommand request, CancellationToken cancellationToken)
    {
        var fromUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var toUserId = request.ToUserId;

        if (fromUserId == toUserId)
        {
            throw new ConflictException("Cannot send a connect request to yourself.");
        }

        var targetUser = await _unitOfWork.Users.GetByIdAsync(toUserId, cancellationToken);

        if (targetUser == null || targetUser.IsDeleted)
        {
            throw new NotFoundException("Target user not found.");
        }

        // Check if blocked
        var isBlocked = await _unitOfWork.Blocks.AnyAsync(b =>
            (b.BlockerUserId == fromUserId && b.BlockedUserId == toUserId) ||
            (b.BlockerUserId == toUserId && b.BlockedUserId == fromUserId), cancellationToken);

        if (isBlocked)
        {
            throw new ForbiddenAccessException("Cannot send connect request to this user.");
        }

        // Check if already connected (enforce UserAId < UserBId rule)
        var userAId = fromUserId.CompareTo(toUserId) < 0 ? fromUserId : toUserId;
        var userBId = fromUserId.CompareTo(toUserId) < 0 ? toUserId : fromUserId;

        if (await _unitOfWork.Connections.AnyAsync(c => c.UserAId == userAId && c.UserBId == userBId, cancellationToken))
        {
            throw new ConflictException("You are already connected with this user.");
        }

        // Check if pending connect request already exists
        var hasPendingRequest = await _unitOfWork.ConnectRequests.AnyAsync(r =>
            r.Status == ConnectRequestStatus.Pending &&
            r.CanonicalUserAId == userAId &&
            r.CanonicalUserBId == userBId, cancellationToken);

        if (hasPendingRequest)
        {
            throw new ConflictException("A pending connect request already exists.");
        }

        var connectRequest = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Status = ConnectRequestStatus.Pending,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow
        };
        connectRequest.SetCanonicalUserIds();

        _unitOfWork.ConnectRequests.Add(connectRequest);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("A pending connect request already exists between these users.");
        }

        return new ConnectRequestDto(
            connectRequest.Id,
            connectRequest.FromUserId,
            connectRequest.ToUserId,
            connectRequest.Status,
            connectRequest.CreatedAt,
            connectRequest.RespondedAt
        );
    }
}
