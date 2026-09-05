using System;

namespace Connect.Application.Features.PresenceSettings.Models;

public class PresenceVisibilityExceptionDto
{
    public Guid TargetUserId { get; set; }
    public bool IsAllowed { get; set; }
}
