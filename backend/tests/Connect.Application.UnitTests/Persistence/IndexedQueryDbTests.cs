using Connect.Infrastructure.Persistence;
using Connect.Infrastructure.Persistence.Repositories;
using Connect.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Connect.Application.UnitTests.Persistence;

public class IndexedQueryDbTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly Repository<Block> _blockRepository;
    private readonly Repository<Connection> _connectionRepository;
    private readonly Repository<User> _userRepository;

    public IndexedQueryDbTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();

        _blockRepository = new Repository<Block>(_dbContext);
        _connectionRepository = new Repository<Connection>(_dbContext);
        _userRepository = new Repository<User>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task<User> CreateUserAsync(string prefix = "user")
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            UserId = $"{prefix}_{id:N}",
            Email = $"{prefix}_{id:N}@example.com",
            PasswordHash = "hashed_password",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    // ==========================================
    // Blocks Repository Tests
    // ==========================================

    [Fact]
    public async Task Blocks_AnyAsync_ReturnsTrueWhenBlockExists_AndFalseWhenNot()
    {
        // Arrange
        var userA = await CreateUserAsync("blocker");
        var userB = await CreateUserAsync("blocked");
        var userC = await CreateUserAsync("other");

        _dbContext.Blocks.Add(new Block
        {
            Id = Guid.NewGuid(),
            BlockerUserId = userA.Id,
            BlockedUserId = userB.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var existsAB = await _blockRepository.AnyAsync(b =>
            (b.BlockerUserId == userA.Id && b.BlockedUserId == userB.Id) ||
            (b.BlockerUserId == userB.Id && b.BlockedUserId == userA.Id));

        var existsAC = await _blockRepository.AnyAsync(b =>
            (b.BlockerUserId == userA.Id && b.BlockedUserId == userC.Id) ||
            (b.BlockerUserId == userC.Id && b.BlockedUserId == userA.Id));

        Assert.True(existsAB, "AnyAsync should return true for existing block between userA and userB.");
        Assert.False(existsAC, "AnyAsync should return false when no block exists between userA and userC.");
    }

    [Fact]
    public async Task Blocks_FirstOrDefaultAsync_ReturnsExactBlockRecord()
    {
        // Arrange
        var userA = await CreateUserAsync("blocker");
        var userB = await CreateUserAsync("blocked");
        var blockId = Guid.NewGuid();

        _dbContext.Blocks.Add(new Block
        {
            Id = blockId,
            BlockerUserId = userA.Id,
            BlockedUserId = userB.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var block = await _blockRepository.FirstOrDefaultAsync(b =>
            b.BlockerUserId == userA.Id && b.BlockedUserId == userB.Id);

        var nonExistentBlock = await _blockRepository.FirstOrDefaultAsync(b =>
            b.BlockerUserId == userB.Id && b.BlockedUserId == userA.Id);

        // Assert
        Assert.NotNull(block);
        Assert.Equal(blockId, block.Id);
        Assert.Null(nonExistentBlock);
    }

    // ==========================================
    // Connections Repository Tests
    // ==========================================

    [Fact]
    public async Task Connections_AnyAsync_ReturnsTrueWhenConnectionExists_AndFalseWhenNot()
    {
        // Arrange
        var u1 = await CreateUserAsync("conn1");
        var u2 = await CreateUserAsync("conn2");
        var u3 = await CreateUserAsync("conn3");

        var userAId = u1.Id.CompareTo(u2.Id) < 0 ? u1.Id : u2.Id;
        var userBId = u1.Id.CompareTo(u2.Id) < 0 ? u2.Id : u1.Id;

        _dbContext.Connections.Add(new Connection
        {
            Id = Guid.NewGuid(),
            UserAId = userAId,
            UserBId = userBId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var exists = await _connectionRepository.AnyAsync(c => c.UserAId == userAId && c.UserBId == userBId);
        var notExists = await _connectionRepository.AnyAsync(c => c.UserAId == u3.Id || c.UserBId == u3.Id);

        Assert.True(exists, "AnyAsync should return true for active connection pair.");
        Assert.False(notExists, "AnyAsync should return false for non-connected pair.");
    }

    [Fact]
    public async Task Connections_FirstOrDefaultAsync_ReturnsExactConnectionRecord()
    {
        // Arrange
        var u1 = await CreateUserAsync("conn1");
        var u2 = await CreateUserAsync("conn2");

        var userAId = u1.Id.CompareTo(u2.Id) < 0 ? u1.Id : u2.Id;
        var userBId = u1.Id.CompareTo(u2.Id) < 0 ? u2.Id : u1.Id;
        var connectionId = Guid.NewGuid();

        _dbContext.Connections.Add(new Connection
        {
            Id = connectionId,
            UserAId = userAId,
            UserBId = userBId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var connection = await _connectionRepository.FirstOrDefaultAsync(c => c.UserAId == userAId && c.UserBId == userBId);
        var missingUserId = Guid.NewGuid();
        var missingConnection = await _connectionRepository.FirstOrDefaultAsync(c => c.UserAId == missingUserId && c.UserBId == userBId);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(connectionId, connection.Id);
        Assert.Null(missingConnection);
    }

    // ==========================================
    // Users Repository Tests
    // ==========================================

    [Fact]
    public async Task Users_AnyAsync_ReturnsTrueForExistingUserIdOrEmail_CaseInsensitive()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserId = "Alice_User",
            Email = "Alice@Example.Com",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act & Assert
        var userIdTaken = await _userRepository.AnyAsync(u => !u.IsDeleted && u.UserId.ToLower() == "alice_user");
        var emailTaken = await _userRepository.AnyAsync(u => !u.IsDeleted && u.Email.ToLower() == "alice@example.com");
        var unknownTaken = await _userRepository.AnyAsync(u => !u.IsDeleted && u.UserId.ToLower() == "bob_user");

        Assert.True(userIdTaken, "AnyAsync should return true for matching UserId case-insensitively.");
        Assert.True(emailTaken, "AnyAsync should return true for matching Email case-insensitively.");
        Assert.False(unknownTaken, "AnyAsync should return false for unregistered UserId.");
    }

    [Fact]
    public async Task Users_FirstOrDefaultAsync_And_GetByIdAsync_ReturnsCorrectUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            UserId = "Bob_Builder",
            Email = "bob@example.com",
            PasswordHash = "hash"
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var foundById = await _userRepository.GetByIdAsync(userId);
        var searchLower = "bob_builder";
        var foundByPredicate = await _userRepository.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == searchLower || u.UserId.ToLower() == searchLower);

        // Assert
        Assert.NotNull(foundById);
        Assert.Equal("Bob_Builder", foundById.UserId);

        Assert.NotNull(foundByPredicate);
        Assert.Equal(userId, foundByPredicate.Id);
    }
}
