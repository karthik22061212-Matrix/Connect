using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Auth.Models;
using MediatR;

namespace Connect.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LoginCommandHandler(
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

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var users = await _unitOfWork.Users.ListAsync(cancellationToken);

        var user = users.FirstOrDefault(u =>
            u.Email.Equals(request.EmailOrUserId, StringComparison.OrdinalIgnoreCase) ||
            u.UserId.Equals(request.EmailOrUserId, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            throw new UnauthorizedAccessException("Invalid email/user ID or password.");
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(user, user.PasswordHash, request.Password);

        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Invalid email/user ID or password.");
        }

        if (user.IsDeleted)
        {
            var now = _dateTimeProvider.UtcNow;
            if (user.ReactivationDeadline.HasValue && user.ReactivationDeadline.Value >= now)
            {
                // Silent reactivation within 60-day window
                user.IsDeleted = false;
                user.DeletedAt = null;
                user.ReactivationDeadline = null;
                user.UpdatedAt = now;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            else
            {
                throw new UnauthorizedAccessException("Account is deactivated and has passed the 60-day reactivation deadline.");
            }
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
