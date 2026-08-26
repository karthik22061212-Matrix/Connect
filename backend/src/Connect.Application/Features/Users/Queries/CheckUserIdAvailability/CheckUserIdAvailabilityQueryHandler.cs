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
        var users = await _unitOfWork.Users.ListAsync(cancellationToken);

        var isAvailable = !users.Any(u =>
            u.UserId.Equals(request.UserId, StringComparison.OrdinalIgnoreCase));

        return new UserIdAvailabilityDto(request.UserId, isAvailable);
    }
}
