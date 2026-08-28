using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Auth.Models;
using MediatR;

namespace Connect.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LoginCommandHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenService refreshTokenService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenService = refreshTokenService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var search = request.EmailOrUserId.ToLower();
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == search || u.UserId.ToLower() == search, cancellationToken);

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
        var refreshToken = await _refreshTokenService.GenerateRefreshTokenAsync(user.Id, cancellationToken);

        return new AuthResponseDto(
            user.Id,
            user.UserId,
            user.Email,
            user.PhoneNumber,
            token,
            refreshToken
        );
    }
}
