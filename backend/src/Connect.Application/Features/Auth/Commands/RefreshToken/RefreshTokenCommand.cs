using Connect.Application.Features.Auth.Models;
using MediatR;

namespace Connect.Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponseDto>;
