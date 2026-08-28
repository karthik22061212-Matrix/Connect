using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Users.Models;
using MediatR;

namespace Connect.Application.Features.Users.Queries.CheckUserIdAvailability;

public class CheckUserIdAvailabilityQueryHandler : IRequestHandler<CheckUserIdAvailabilityQuery, UserIdAvailabilityDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CheckUserIdAvailabilityQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserIdAvailabilityDto> Handle(CheckUserIdAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var userIdLower = request.UserId.ToLower();
        var isTaken = await _unitOfWork.Users.AnyAsync(
            u => !u.IsDeleted && u.UserId.ToLower() == userIdLower, cancellationToken);

        return new UserIdAvailabilityDto(request.UserId, !isTaken);
    }
}
