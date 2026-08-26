using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Models;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.ConnectRequests.Commands.AcceptConnectRequest;

public class AcceptConnectRequestCommandHandler : IRequestHandler<AcceptConnectRequestCommand, ConnectRequestDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AcceptConnectRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ConnectRequestDto> Handle(AcceptConnectRequestCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var connectRequests = await _unitOfWork.ConnectRequests.ListAsync(cancellationToken);
        var connectReq = connectRequests.FirstOrDefault(r => r.Id == request.RequestId);

        if (connectReq == null)
        {
            throw new NotFoundException("Connect request not found.");
        }

        if (connectReq.ToUserId != currentUserId)
        {
            throw new ForbiddenAccessException("You can only accept connect requests sent to you.");
        }

        if (connectReq.Status != ConnectRequestStatus.Pending)
        {
            throw new ConflictException("Connect request has already been responded to.");
        }

        connectReq.Status = ConnectRequestStatus.Accepted;
        connectReq.RespondedAt = _dateTimeProvider.UtcNow;
        connectReq.UpdatedAt = _dateTimeProvider.UtcNow;

        // Enforce Connection ordering rule: UserAId < UserBId
        var userAId = connectReq.FromUserId.CompareTo(connectReq.ToUserId) < 0 ? connectReq.FromUserId : connectReq.ToUserId;
        var userBId = connectReq.FromUserId.CompareTo(connectReq.ToUserId) < 0 ? connectReq.ToUserId : connectReq.FromUserId;

        var connections = await _unitOfWork.Connections.ListAsync(cancellationToken);
        var existingConnection = connections.FirstOrDefault(c => c.UserAId == userAId && c.UserBId == userBId);

        if (existingConnection == null)
        {
            var newConnection = new Connection
            {
                Id = Guid.NewGuid(),
                UserAId = userAId,
                UserBId = userBId,
                CreatedAt = _dateTimeProvider.UtcNow,
                UpdatedAt = _dateTimeProvider.UtcNow
            };

            _unitOfWork.Connections.Add(newConnection);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Ignore DB duplicate constraint if connection already exists
        }

        return new ConnectRequestDto(
            connectReq.Id,
            connectReq.FromUserId,
            connectReq.ToUserId,
            connectReq.Status,
            connectReq.CreatedAt,
            connectReq.RespondedAt
        );
    }
}
