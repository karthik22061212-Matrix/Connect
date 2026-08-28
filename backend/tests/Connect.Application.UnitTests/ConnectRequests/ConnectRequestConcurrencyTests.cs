using Connect.Application.Common.Exceptions;
using Connect.Application.Common.Interfaces;
using Connect.Application.Features.ConnectRequests.Commands.SendConnectRequest;
using Connect.Application.Features.ConnectRequests.Models;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Persistence;
using Connect.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Connect.Application.UnitTests.ConnectRequests;

public class ConnectRequestConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ConnectRequestConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new ApplicationDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private async Task<(User user1, User user2)> SeedUsersAsync(ApplicationDbContext db)
    {
        var user1 = new User { Id = Guid.NewGuid(), UserId = "user1", Email = "user1@test.com", PasswordHash = "hash" };
        var user2 = new User { Id = Guid.NewGuid(), UserId = "user2", Email = "user2@test.com", PasswordHash = "hash" };

        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        return (user1, user2);
    }

    [Fact]
    public async Task EFCore_OppositeDirectionConnectRequests_UniqueIndex_RejectsSecondInsertWithDbUpdateException()
    {
        using var setupDb = new ApplicationDbContext(_options);
        var (user1, user2) = await SeedUsersAsync(setupDb);

        using var db1 = new ApplicationDbContext(_options);
        using var db2 = new ApplicationDbContext(_options);

        // User 1 -> User 2 (Pending)
        var req1 = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = user1.Id,
            ToUserId = user2.Id,
            Status = ConnectRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        req1.SetCanonicalUserIds();
        db1.ConnectRequests.Add(req1);
        await db1.SaveChangesAsync();

        // User 2 -> User 1 (Pending in opposite direction)
        var req2 = new ConnectRequest
        {
            Id = Guid.NewGuid(),
            FromUserId = user2.Id,
            ToUserId = user1.Id,
            Status = ConnectRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        req2.SetCanonicalUserIds();
        db2.ConnectRequests.Add(req2);

        // Prove that DB-level unique index throws real DbUpdateException for opposite-direction insert
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await db2.SaveChangesAsync();
        });

        // Confirm only 1 pending request exists in DB
        using var verifyDb = new ApplicationDbContext(_options);
        var count = await verifyDb.ConnectRequests.CountAsync(r => r.Status == ConnectRequestStatus.Pending);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SendConnectRequestCommandHandler_ConcurrentOppositeRequests_OnlyOneSurvivesAndOtherThrowsConflict()
    {
        using var setupDb = new ApplicationDbContext(_options);
        var (user1, user2) = await SeedUsersAsync(setupDb);

        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        mockDateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        // Simulate Handler 1 (User 1 -> User 2)
        using var db1 = new ApplicationDbContext(_options);
        var unitOfWork1 = new UnitOfWork(db1);
        var mockCurrentUserService1 = new Mock<ICurrentUserService>();
        mockCurrentUserService1.Setup(c => c.UserId).Returns(user1.Id);

        var handler1 = new SendConnectRequestCommandHandler(
            unitOfWork1,
            mockCurrentUserService1.Object,
            mockDateTimeProvider.Object);

        // Simulate Handler 2 (User 2 -> User 1)
        using var db2 = new ApplicationDbContext(_options);
        var unitOfWork2 = new UnitOfWork(db2);
        var mockCurrentUserService2 = new Mock<ICurrentUserService>();
        mockCurrentUserService2.Setup(c => c.UserId).Returns(user2.Id);

        var handler2 = new SendConnectRequestCommandHandler(
            unitOfWork2,
            mockCurrentUserService2.Object,
            mockDateTimeProvider.Object);

        // Both pass pre-checks if executed before either calls SaveChangesAsync
        var task1 = handler1.Handle(new SendConnectRequestCommand(user2.Id), CancellationToken.None);
        var task2 = handler2.Handle(new SendConnectRequestCommand(user1.Id), CancellationToken.None);

        var results = await Task.WhenAll(
            Task.Run(async () => { try { return (success: true, dto: (ConnectRequestDto?)await task1, ex: (Exception?)null); } catch (Exception ex) { return (false, null, ex); } }),
            Task.Run(async () => { try { return (success: true, dto: (ConnectRequestDto?)await task2, ex: (Exception?)null); } catch (Exception ex) { return (false, null, ex); } })
        );

        // Assert exactly one handler succeeded and one threw ConflictException
        var succeeded = results.Count(r => r.success);
        var failedWithConflict = results.Count(r => !r.success && r.ex is ConflictException);

        Assert.Equal(1, succeeded);
        Assert.Equal(1, failedWithConflict);

        // Verify database contains only 1 pending request
        using var verifyDb = new ApplicationDbContext(_options);
        var pendingRequests = await verifyDb.ConnectRequests.Where(r => r.Status == ConnectRequestStatus.Pending).ToListAsync();
        Assert.Single(pendingRequests);
    }
}
