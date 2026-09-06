using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.PresenceSettings.Queries.GetPresenceSettings;

public record GetPresenceSettingsQuery : IRequest<PresenceVisibility>;
