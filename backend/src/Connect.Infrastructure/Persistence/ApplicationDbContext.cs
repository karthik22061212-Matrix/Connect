using System.Reflection;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Connect.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ConnectRequest> ConnectRequests => Set<ConnectRequest>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<Call> Calls => Set<Call>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
