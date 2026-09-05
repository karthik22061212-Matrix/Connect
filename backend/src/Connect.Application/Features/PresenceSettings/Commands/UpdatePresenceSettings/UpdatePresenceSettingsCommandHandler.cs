using System;
using System.Threading;
using System.Threading.Tasks;
using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Features.PresenceSettings.Commands.UpdatePresenceSettings;

public class UpdatePresenceSettingsCommandHandler : IRequestHandler<UpdatePresenceSettingsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdatePresenceSettingsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(UpdatePresenceSettingsCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserService.UserId ?? throw new UnauthorizedAccessException();

        var setting = await _context.UserPresenceSettings
            .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);

        if (setting == null)
        {
            setting = new UserPresenceSetting
            {
                UserId = currentUserId,
                PresenceVisibility = request.Visibility
            };
            _context.UserPresenceSettings.Add(setting);
        }
        else
        {
            setting.PresenceVisibility = request.Visibility;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
