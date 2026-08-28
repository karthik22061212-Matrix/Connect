using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Models;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.ConnectRequests.Commands.DeclineConnectRequest;

public class DeclineConnectRequestCommandHandler : IRequestHandler<DeclineConnectRequestCommand, ConnectRequestDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DeclineConnectRequestCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ConnectRequestDto> Handle(DeclineConnectRequestCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var connectReq = await _unitOfWork.ConnectRequests.GetByIdAsync(request.RequestId, cancellationToken);

        if (connectReq == null)
        {
            throw new NotFoundException("Connect request not found.");
        }

        if (connectReq.ToUserId != currentUserId)
        {
            throw new ForbiddenAccessException("You can only decline connect requests sent to you.");
        }

        if (connectReq.Status != ConnectRequestStatus.Pending)
        {
            throw new ConflictException("Connect request has already been responded to.");
        }

        connectReq.Status = ConnectRequestStatus.Declined;
        connectReq.RespondedAt = _dateTimeProvider.UtcNow;
        connectReq.UpdatedAt = _dateTimeProvider.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
