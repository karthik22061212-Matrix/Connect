namespace Connect.Application.Common.Interfaces;

public interface ICallTimeoutProcessor
{
    Task ProcessExpiredTimeoutsAsync(CancellationToken cancellationToken = default);
}
