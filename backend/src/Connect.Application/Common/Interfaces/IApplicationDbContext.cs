using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Connect.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<ConnectRequest> ConnectRequests { get; }
    DbSet<Connection> Connections { get; }
    DbSet<Call> Calls { get; }
    DbSet<Block> Blocks { get; }
    DbSet<Report> Reports { get; }
    DbSet<DeviceToken> DeviceTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
