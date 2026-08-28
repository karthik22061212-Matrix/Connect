using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.Blocking.Commands.BlockUser;

public class BlockUserCommandHandler : IRequestHandler<BlockUserCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlockUserCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<bool> Handle(BlockUserCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        if (currentUserId.Value == request.UserIdToBlock)
        {
            throw new ConflictException("Cannot block yourself.");
        }

        var targetUser = await _unitOfWork.Users.GetByIdAsync(request.UserIdToBlock, cancellationToken);
        if (targetUser == null || targetUser.IsDeleted)
        {
            throw new NotFoundException("User to block not found.");
        }

        var alreadyBlocked = await _unitOfWork.Blocks.AnyAsync(
            b => b.BlockerUserId == currentUserId.Value && b.BlockedUserId == request.UserIdToBlock, cancellationToken);
        if (alreadyBlocked)
        {
            throw new ConflictException("User is already blocked.");
        }

        var block = new Block
        {
            Id = Guid.NewGuid(),
            BlockerUserId = currentUserId.Value,
            BlockedUserId = request.UserIdToBlock,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow
        };

        _unitOfWork.Blocks.Add(block);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("User is already blocked.");
        }

        return true;
    }
}
