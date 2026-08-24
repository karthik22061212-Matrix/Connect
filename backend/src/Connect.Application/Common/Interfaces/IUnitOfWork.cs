using Connect.Domain.Entities;

namespace Connect.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<User> Users { get; }
    IRepository<ConnectRequest> ConnectRequests { get; }
    IRepository<Connection> Connections { get; }
    IRepository<Call> Calls { get; }
    IRepository<Block> Blocks { get; }
    IRepository<Report> Reports { get; }
    IRepository<DeviceToken> DeviceTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
