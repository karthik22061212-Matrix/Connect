using Connect.Application.Common.Interfaces;
using Connect.Application.Features.Calls.Commands.EndCall;
using Connect.Application.Features.Calls.Commands.FailCall;
using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Connect.Infrastructure.Persistence;
using Connect.Infrastructure.Realtime;
using Connect.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Connect.Application.UnitTests.Calls;

public class CallConcurrencyTests : IDisposable
{
    private class TestApplicationDbContext : ApplicationDbContext
    {
        public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Call>().Property(c => c.RowVersion).IsRowVersion().HasDefaultValue(new byte[] { 0, 0, 0, 1 });
        }
    }

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public CallConcurrencyTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new TestApplicationDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private async Task<(User caller, User callee, Connection connection, Call call)> SeedDatabaseAsync(ApplicationDbContext db)
    {
        var caller = new User { Id = Guid.NewGuid(), UserId = "caller", Email = "caller@test.com", PasswordHash = "hash" };
        var callee = new User { Id = Guid.NewGuid(), UserId = "callee", Email = "callee@test.com", PasswordHash = "hash" };
        var connection = new Connection { Id = Guid.NewGuid(), UserAId = caller.Id, UserBId = callee.Id };
        var call = new Call
        {
            Id = Guid.NewGuid(),
            ConnectionId = connection.Id,
            CallerId = caller.Id,
            CalleeId = callee.Id,
            Status = CallStatus.Ringing,
            RowVersion = new byte[] { 0, 0, 0, 1 }
        };

        db.Users.AddRange(caller, callee);
        db.Connections.Add(connection);
        db.Calls.Add(call);
        await db.SaveChangesAsync();

        return (caller, callee, connection, call);
    }

    [Fact]
    public async Task EFCore_TwoTrackedInstances_FirstSaveSucceeds_SecondSaveThrowsDbUpdateConcurrencyException()
    {
        using var setupDb = new TestApplicationDbContext(_options);
        var (_, _, _, seededCall) = await SeedDatabaseAsync(setupDb);

        using var context1 = new TestApplicationDbContext(_options);
        using var context2 = new TestApplicationDbContext(_options);

        var callInstance1 = await context1.Calls.SingleAsync(c => c.Id == seededCall.Id);
        var callInstance2 = await context2.Calls.SingleAsync(c => c.Id == seededCall.Id);

        callInstance1.Status = CallStatus.Completed;
        await context1.SaveChangesAsync();

        // Simulate SQL Server automatic rowversion column update upon modification
        await context1.Database.ExecuteSqlRawAsync("UPDATE Calls SET RowVersion = x'00000002' WHERE Id = {0}", seededCall.Id);

        callInstance2.Status = CallStatus.Failed;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            await context2.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task EndCallCommandHandler_OnConcurrencyException_HandlesGracefullyWithoutThrowing()
    {
        using var setupDb = new TestApplicationDbContext(_options);
        var (caller, callee, _, call) = await SeedDatabaseAsync(setupDb);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCurrentUserService = new Mock<ICurrentUserService>();
        var mockPresenceTracker = new Mock<IPresenceTracker>();
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();

        mockCurrentUserService.Setup(s => s.UserId).Returns(caller.Id);
        mockDateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        // Simulate SaveChangesAsync throwing DbUpdateConcurrencyException on the tracked call entity entry
        using var trackDb = new TestApplicationDbContext(_options);
        var trackedCall = await trackDb.Calls.SingleAsync(c => c.Id == call.Id);

        mockUnitOfWork.Setup(u => u.Calls.GetByIdAsync(call.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackedCall);

        var entry = (IUpdateEntry)trackDb.Entry(trackedCall).GetInfrastructure();
        mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict", new List<IUpdateEntry> { entry }));

        var handler = new EndCallCommandHandler(
            mockUnitOfWork.Object,
            mockCurrentUserService.Object,
            mockPresenceTracker.Object,
            mockDateTimeProvider.Object);

        var result = await handler.Handle(new EndCallCommand(call.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(call.Id, result.CallId);
        Assert.Equal(caller.Id, result.CallerId);
        Assert.Equal(callee.Id, result.CalleeId);
    }

    [Fact]
    public async Task FailCallCommandHandler_OnConcurrencyException_HandlesGracefullyWithoutThrowing()
    {
        using var setupDb = new TestApplicationDbContext(_options);
        var (caller, callee, _, call) = await SeedDatabaseAsync(setupDb);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockCurrentUserService = new Mock<ICurrentUserService>();
        var mockPresenceTracker = new Mock<IPresenceTracker>();
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();

        mockCurrentUserService.Setup(s => s.UserId).Returns(caller.Id);
        mockDateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);

        using var trackDb = new TestApplicationDbContext(_options);
        var trackedCall = await trackDb.Calls.SingleAsync(c => c.Id == call.Id);

        mockUnitOfWork.Setup(u => u.Calls.GetByIdAsync(call.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trackedCall);

        var entry = (IUpdateEntry)trackDb.Entry(trackedCall).GetInfrastructure();
        mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict", new List<IUpdateEntry> { entry }));

        var handler = new FailCallCommandHandler(
            mockUnitOfWork.Object,
            mockCurrentUserService.Object,
            mockPresenceTracker.Object,
            mockDateTimeProvider.Object);

        var result = await handler.Handle(new FailCallCommand(call.Id, MissedReason.ConnectionFailed), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(call.Id, result.CallId);
        Assert.Equal(caller.Id, result.CallerId);
    }

    [Fact]
    public async Task CallTimeoutProcessor_OnConcurrencyException_HandlesGracefullyAndNoOps()
    {
        using var setupDb = new TestApplicationDbContext(_options);
        var (_, _, _, call) = await SeedDatabaseAsync(setupDb);
        call.TimeoutDeadline = DateTime.UtcNow.AddSeconds(-5);
        call.TimeoutType = CallTimeoutType.Ring;
        setupDb.SaveChanges();

        using var testDb = new TestApplicationDbContext(_options);
        var trackedCall = await testDb.Calls.SingleAsync(c => c.Id == call.Id);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockDateTimeProvider = new Mock<IDateTimeProvider>();
        var mockPresenceTracker = new Mock<IPresenceTracker>();
        var mockHubContext = new Mock<IHubContext<CallHub, ICallHubClient>>();
        var mockPushNotification = new Mock<IPushNotificationService>();
        var mockLogger = new Mock<ILogger<CallTimeoutProcessor>>();

        mockDateTimeProvider.Setup(d => d.UtcNow).Returns(DateTime.UtcNow);
        mockUnitOfWork.Setup(u => u.Calls.ListAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Call, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Call> { trackedCall });

        var entry = (IUpdateEntry)testDb.Entry(trackedCall).GetInfrastructure();
        mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => { trackedCall.Status = CallStatus.Completed; })
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict", new List<IUpdateEntry> { entry }));

        var processor = new CallTimeoutProcessor(
            mockUnitOfWork.Object,
            mockDateTimeProvider.Object,
            mockPresenceTracker.Object,
            mockHubContext.Object,
            mockPushNotification.Object,
            mockLogger.Object);

        var exception = await Record.ExceptionAsync(() => processor.ProcessExpiredTimeoutsAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
