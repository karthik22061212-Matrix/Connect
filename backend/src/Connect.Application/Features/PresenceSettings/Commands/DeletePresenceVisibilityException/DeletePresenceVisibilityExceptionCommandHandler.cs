using System;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.PresenceSettings.Commands.DeletePresenceVisibilityException;

public class DeletePresenceVisibilityExceptionCommandHandler : IRequestHandler<DeletePresenceVisibilityExceptionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeletePresenceVisibilityExceptionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(DeletePresenceVisibilityExceptionCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var exception = await _context.PresenceVisibilityExceptions
            .FirstOrDefaultAsync(e => e.OwnerUserId == currentUserId && e.TargetUserId == request.TargetUserId, cancellationToken);

        if (exception != null)
        {
            _context.PresenceVisibilityExceptions.Remove(exception);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
