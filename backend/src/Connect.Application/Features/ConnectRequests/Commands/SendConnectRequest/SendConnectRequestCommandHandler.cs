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

        var users = await _unitOfWork.Users.ListAsync(cancellationToken);
        var targetUser = users.FirstOrDefault(u => u.Id == toUserId && !u.IsDeleted);

        if (targetUser == null)
        {
            throw new NotFoundException("Target user not found.");
        }

        // Check if blocked
        var blocks = await _unitOfWork.Blocks.ListAsync(cancellationToken);
        var isBlocked = blocks.Any(b =>
            (b.BlockerUserId == fromUserId && b.BlockedUserId == toUserId) ||
            (b.BlockerUserId == toUserId && b.BlockedUserId == fromUserId));

        if (isBlocked)
        {
            throw new ForbiddenAccessException("Cannot send connect request to this user.");
        }

        // Check if already connected (enforce UserAId < UserBId rule)
        var userAId = fromUserId.CompareTo(toUserId) < 0 ? fromUserId : toUserId;
        var userBId = fromUserId.CompareTo(toUserId) < 0 ? toUserId : fromUserId;

        var connections = await _unitOfWork.Connections.ListAsync(cancellationToken);
        if (connections.Any(c => c.UserAId == userAId && c.UserBId == userBId))
        {
            throw new ConflictException("You are already connected with this user.");
        }

        // Check if pending connect request already exists
        var existingRequests = await _unitOfWork.ConnectRequests.ListAsync(cancellationToken);
        if (existingRequests.Any(r =>
            r.Status == ConnectRequestStatus.Pending &&
            ((r.FromUserId == fromUserId && r.ToUserId == toUserId) ||
             (r.FromUserId == toUserId && r.ToUserId == fromUserId))))
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
