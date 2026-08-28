using Connect.Domain.Entities;

namespace Connect.Application.Common.Interfaces;

public interface IRefreshTokenService
{
    Task<string> GenerateRefreshTokenAsync(Guid userId, CancellationToken ct = default);
    Task<(string NewRefreshToken, User User)> ValidateAndRotateAsync(string plaintextToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string plaintextToken, CancellationToken ct = default);
}
