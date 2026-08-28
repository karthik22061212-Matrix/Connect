using System.Security.Cryptography;
using System.Text;
using Connect.Application.Common.Interfaces;
using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Connect.Infrastructure.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public RefreshTokenService(IApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var plaintextToken = GenerateRandomTokenString();
        var hash = HashToken(plaintextToken);
        var expiryDays = GetRefreshTokenExpiryDays();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(ct);

        return plaintextToken;
    }

    public async Task<(string NewRefreshToken, User User)> ValidateAndRotateAsync(string plaintextToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var hash = HashToken(plaintextToken);
        var existingToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (existingToken == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (existingToken.User == null || existingToken.User.IsDeleted)
        {
            throw new UnauthorizedAccessException("User not found or account deactivated.");
        }

        // Reuse / Theft Detection: Token has already been revoked!
        if (existingToken.RevokedAt.HasValue)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var token in activeTokens)
            {
                token.RevokedAt = now;
            }

            await _context.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Refresh token reuse detected. All sessions revoked for security.");
        }

        // Expiry check
        if (existingToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        // Token is valid: Rotate it
        var newPlaintextToken = GenerateRandomTokenString();
        var newHash = HashToken(newPlaintextToken);
        var expiryDays = GetRefreshTokenExpiryDays();

        var newToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            TokenHash = newHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays)
        };

        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.ReplacedByTokenId = newToken.Id;

        _context.RefreshTokens.Add(newToken);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.RefreshTokens.Entry(newToken).State = EntityState.Detached;
            _context.RefreshTokens.Entry(existingToken).State = EntityState.Detached;

            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                .ToListAsync(ct);

            var revokeNow = DateTime.UtcNow;
            foreach (var token in activeTokens)
            {
                token.RevokedAt = revokeNow;
            }

            await _context.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Refresh token reuse detected. All sessions revoked for security.");
        }

        return (newPlaintextToken, existingToken.User);
    }

    public async Task RevokeRefreshTokenAsync(string plaintextToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var hash = HashToken(plaintextToken);
        var existingToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (existingToken == null || existingToken.User == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        if (existingToken.RevokedAt == null)
        {
            existingToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    private static string GenerateRandomTokenString()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private int GetRefreshTokenExpiryDays()
    {
        var daysStr = _configuration["JwtSettings:RefreshTokenExpiryDays"];
        return int.TryParse(daysStr, out var days) ? days : 30;
    }
}
