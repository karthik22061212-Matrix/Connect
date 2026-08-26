using Connect.Application.Common.Interfaces;
using MediatR;

namespace Connect.Application.Features.Account.Commands.PurgeOldCallHistory;

public class PurgeOldCallHistoryCommandHandler : IRequestHandler<PurgeOldCallHistoryCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PurgeOldCallHistoryCommandHandler(
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<int> Handle(PurgeOldCallHistoryCommand request, CancellationToken cancellationToken)
    {
        var cutoffDate = _dateTimeProvider.UtcNow.AddDays(-90);
        var expiredCalls = await _unitOfWork.Calls.ListAsync(c => c.StartedAt < cutoffDate, cancellationToken);

        if (expiredCalls.Count == 0)
        {
            return 0;
        }

        foreach (var call in expiredCalls)
        {
            _unitOfWork.Calls.Remove(call);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expiredCalls.Count;
    }
}
