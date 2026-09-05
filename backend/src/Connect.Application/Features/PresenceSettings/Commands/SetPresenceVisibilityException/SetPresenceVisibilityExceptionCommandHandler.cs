using System;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.PresenceSettings.Commands.SetPresenceVisibilityException;

public class SetPresenceVisibilityExceptionCommandHandler : IRequestHandler<SetPresenceVisibilityExceptionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SetPresenceVisibilityExceptionCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(SetPresenceVisibilityExceptionCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        if (currentUserId == request.TargetUserId)
        {
            throw new Connect.Application.Common.Exceptions.ForbiddenAccessException("Cannot set a presence visibility exception for yourself.");
        }

        var targetUserExists = await _context.Users.AnyAsync(u => u.Id == request.TargetUserId && !u.IsDeleted, cancellationToken);
        if (!targetUserExists)
        {
            throw new NotFoundException(nameof(User), request.TargetUserId);
        }

        var exception = await _context.PresenceVisibilityExceptions
            .FirstOrDefaultAsync(e => e.OwnerUserId == currentUserId && e.TargetUserId == request.TargetUserId, cancellationToken);

        if (exception == null)
        {
            exception = new PresenceVisibilityException
            {
                OwnerUserId = currentUserId,
                TargetUserId = request.TargetUserId,
                IsAllowed = request.IsAllowed
            };
            _context.PresenceVisibilityExceptions.Add(exception);
        }
        else
        {
            exception.IsAllowed = request.IsAllowed;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
