using Connect.Application.Common.Interfaces;
using MediatR;

namespace Connect.Application.Features.Account.Commands.PurgeExpiredAccounts;

public class PurgeExpiredAccountsCommandHandler : IRequestHandler<PurgeExpiredAccountsCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PurgeExpiredAccountsCommandHandler(
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<int> Handle(PurgeExpiredAccountsCommand request, CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;
        var expiredUsers = await _unitOfWork.Users.ListAsync(
            u => u.IsDeleted && u.ReactivationDeadline.HasValue && u.ReactivationDeadline.Value < now,
            cancellationToken);

        if (expiredUsers.Count == 0)
        {
            return 0;
        }

        foreach (var user in expiredUsers)
        {
            _unitOfWork.Users.Remove(user);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expiredUsers.Count;
    }
}
