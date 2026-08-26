using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Auth.Models;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using MediatR;

namespace Connect.Application.Features.Auth.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, AuthResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RegisterUserCommandHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AuthResponseDto> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var users = await _unitOfWork.Users.ListAsync(cancellationToken);

        if (users.Any(u => u.UserId.Equals(request.UserId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"User ID '{request.UserId}' is already taken.");
        }

        if (users.Any(u => u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"Email '{request.Email}' is already registered.");
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            users.Any(u => u.PhoneNumber != null && u.PhoneNumber.Equals(request.PhoneNumber, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("Phone number is already registered.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Email = request.Email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber,
            PresenceStatus = PresenceStatus.Offline,
            SubscriptionTier = SubscriptionTier.Free,
            CreatedAt = _dateTimeProvider.UtcNow,
            UpdatedAt = _dateTimeProvider.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        _unitOfWork.Users.Add(user);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            throw new ConflictException($"User ID '{request.UserId}' or Email '{request.Email}' is already taken.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponseDto(
            user.Id,
            user.UserId,
            user.Email,
            user.PhoneNumber,
            token
        );
    }
}
