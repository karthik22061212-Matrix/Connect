using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.PresenceSettings.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.PresenceSettings.Queries.GetPresenceVisibilityExceptions;

public class GetPresenceVisibilityExceptionsQueryHandler : IRequestHandler<GetPresenceVisibilityExceptionsQuery, List<PresenceVisibilityExceptionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPresenceVisibilityExceptionsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<PresenceVisibilityExceptionDto>> Handle(GetPresenceVisibilityExceptionsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var exceptions = await _context.PresenceVisibilityExceptions
            .Where(e => e.OwnerUserId == currentUserId)
            .Select(e => new PresenceVisibilityExceptionDto
            {
                TargetUserId = e.TargetUserId,
                IsAllowed = e.IsAllowed
            })
            .ToListAsync(cancellationToken);

        return exceptions;
    }
}
