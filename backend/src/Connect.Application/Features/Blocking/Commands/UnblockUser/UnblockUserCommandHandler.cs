using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using MediatR;

namespace Connect.Application.Features.Blocking.Commands.UnblockUser;

public class UnblockUserCommandHandler : IRequestHandler<UnblockUserCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UnblockUserCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(UnblockUserCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var blocks = await _unitOfWork.Blocks.ListAsync(cancellationToken);
        var block = blocks.FirstOrDefault(b => b.BlockerUserId == currentUserId.Value && b.BlockedUserId == request.UserIdToUnblock);

        if (block == null)
        {
            throw new NotFoundException("Block record not found.");
        }

        _unitOfWork.Blocks.Remove(block);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
