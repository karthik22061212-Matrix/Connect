using Connect.Application.Features.Auth.Models;
using MediatR;

namespace Connect.Application.Features.Auth.Commands.RegisterUser;

public record RegisterUserCommand(
    string UserId,
    string Email,
    string Password,
    string? PhoneNumber = null
) : IRequest<AuthResponseDto>;
