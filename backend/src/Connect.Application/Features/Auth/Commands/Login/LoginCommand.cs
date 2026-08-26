using Connect.Application.Features.Auth.Models;
using MediatR;

namespace Connect.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string EmailOrUserId,
    string Password
) : IRequest<AuthResponseDto>;
