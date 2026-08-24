using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Connect.Infrastructure.Persistence.Repositories;

namespace Connect.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    private IRepository<User>? _users;
    private IRepository<ConnectRequest>? _connectRequests;
    private IRepository<Connection>? _connections;
    private IRepository<Call>? _calls;
    private IRepository<Block>? _blocks;
    private IRepository<Report>? _reports;
    private IRepository<DeviceToken>? _deviceTokens;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IRepository<User> Users => _users ??= new Repository<User>(_dbContext);
    public IRepository<ConnectRequest> ConnectRequests => _connectRequests ??= new Repository<ConnectRequest>(_dbContext);
    public IRepository<Connection> Connections => _connections ??= new Repository<Connection>(_dbContext);
    public IRepository<Call> Calls => _calls ??= new Repository<Call>(_dbContext);
    public IRepository<Block> Blocks => _blocks ??= new Repository<Block>(_dbContext);
    public IRepository<Report> Reports => _reports ??= new Repository<Report>(_dbContext);
    public IRepository<DeviceToken> DeviceTokens => _deviceTokens ??= new Repository<DeviceToken>(_dbContext);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _dbContext.SaveChangesAsync(ct);
    }
}
