using System.Collections.Generic;
using Connect.Application.Features.PresenceSettings.Models;
using MediatR;

namespace Connect.Application.Features.PresenceSettings.Queries.GetPresenceVisibilityExceptions;

public record GetPresenceVisibilityExceptionsQuery : IRequest<List<PresenceVisibilityExceptionDto>>;
