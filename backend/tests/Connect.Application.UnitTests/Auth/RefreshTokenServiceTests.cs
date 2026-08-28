using Connect.Domain.Entities;
using Connect.Infrastructure.Persistence;
using Connect.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Connect.Application.UnitTests.Auth;

public class RefreshTokenServiceTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private IConfiguration CreateConfiguration(int expiryDays = 30)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "JwtSettings:RefreshTokenExpiryDays", expiryDays.ToString() }
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task ValidateAndRotate_ValidToken_SucceedsAndRotates()
    {
        using var db = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), UserId = "john", Email = "john@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new RefreshTokenService(db, CreateConfiguration());
        var token = await service.GenerateRefreshTokenAsync(user.Id);

        var (newToken, returnedUser) = await service.ValidateAndRotateAsync(token);

        Assert.NotNull(newToken);
        Assert.NotEqual(token, newToken);
        Assert.Equal(user.Id, returnedUser.Id);

        var tokensInDb = await db.RefreshTokens.ToListAsync();
        Assert.Equal(2, tokensInDb.Count);

        var oldToken = tokensInDb.Single(t => t.RevokedAt != null);
        var activeToken = tokensInDb.Single(t => t.RevokedAt == null);

        Assert.Equal(activeToken.Id, oldToken.ReplacedByTokenId);
    }

    [Fact]
    public async Task ValidateAndRotate_RevokedToken_FailsAndRevokesEntireFamily()
    {
        using var db = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), UserId = "jane", Email = "jane@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new RefreshTokenService(db, CreateConfiguration());

        // Token 1 -> rotated to Token 2 -> rotated to Token 3
        var token1 = await service.GenerateRefreshTokenAsync(user.Id);
        var (token2, _) = await service.ValidateAndRotateAsync(token1);
        var (token3, _) = await service.ValidateAndRotateAsync(token2);

        // Attempt to reuse token1 (already revoked)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ValidateAndRotateAsync(token1));

        // Verify that ALL tokens for user are now revoked
        var activeTokens = await db.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.RevokedAt == null).ToListAsync();
        Assert.Empty(activeTokens);
    }

    [Fact]
    public async Task ValidateAndRotate_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), UserId = "bob", Email = "bob@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new RefreshTokenService(db, CreateConfiguration(-1)); // Expired immediately
        var token = await service.GenerateRefreshTokenAsync(user.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ValidateAndRotateAsync(token));
    }

    [Fact]
    public async Task RevokeRefreshToken_Logout_RevokesOnlySpecificTokenNotFamily()
    {
        using var db = CreateDbContext();
        var user = new User { Id = Guid.NewGuid(), UserId = "alice", Email = "alice@example.com", PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new RefreshTokenService(db, CreateConfiguration());

        var token1 = await service.GenerateRefreshTokenAsync(user.Id);
        var token2 = await service.GenerateRefreshTokenAsync(user.Id); // Second session token

        await service.RevokeRefreshTokenAsync(token1);

        var dbToken1 = await db.RefreshTokens.SingleAsync(t => t.TokenHash != null && t.Id == db.RefreshTokens.First(x => x.UserId == user.Id && x.RevokedAt != null).Id);
        var dbToken2 = await db.RefreshTokens.SingleAsync(t => t.UserId == user.Id && t.RevokedAt == null);

        Assert.NotNull(dbToken1.RevokedAt);
        Assert.Null(dbToken2.RevokedAt);
    }

    [Fact]
    public async Task ValidateAndRotate_ConcurrentCallsWithSameToken_ExactlyOneSucceedsAndOtherIsRejected()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var setupDb = new ApplicationDbContext(options))
        {
            await setupDb.Database.EnsureCreatedAsync();
            var user = new User { Id = Guid.NewGuid(), UserId = "concurrent_user", Email = "concurrent@example.com", PasswordHash = "hash" };
            setupDb.Users.Add(user);
            await setupDb.SaveChangesAsync();

            var setupService = new RefreshTokenService(setupDb, CreateConfiguration());
            var token = await setupService.GenerateRefreshTokenAsync(user.Id);

            using var db1 = new ApplicationDbContext(options);
            using var db2 = new ApplicationDbContext(options);

            var service1 = new RefreshTokenService(db1, CreateConfiguration());
            var service2 = new RefreshTokenService(db2, CreateConfiguration());

            var task1 = service1.ValidateAndRotateAsync(token);
            var task2 = service2.ValidateAndRotateAsync(token);

            try
            {
                await Task.WhenAll(task1, task2);
            }
            catch
            {
                // Task.WhenAll throws if any task faults
            }

            var task1Succeeded = task1.Status == TaskStatus.RanToCompletion;
            var task2Succeeded = task2.Status == TaskStatus.RanToCompletion;

            Assert.True(task1Succeeded ^ task2Succeeded, "Exactly one concurrent call must succeed and the other must fail.");

            var failedTask = task1Succeeded ? task2 : task1;
            Assert.True(failedTask.IsFaulted);
            Assert.IsType<UnauthorizedAccessException>(failedTask.Exception?.InnerException);

            var succeededTask = task1Succeeded ? task1 : task2;
            var (newToken, returnedUser) = await succeededTask;
            Assert.NotNull(newToken);
            Assert.Equal(user.Id, returnedUser.Id);
        }
    }
}
