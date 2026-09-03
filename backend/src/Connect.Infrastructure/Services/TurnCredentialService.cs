using System;
using System.Security.Cryptography;
using System.Text;
using Connect.Application.Common.Interfaces;
using Connect.Application.Common.Models;
using Connect.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Connect.Infrastructure.Services;

public class TurnCredentialService : ITurnCredentialService
{
    private readonly TurnSettings _settings;

    public TurnCredentialService(IOptions<TurnSettings> options)
    {
        _settings = options.Value;
    }

    public TurnCredentialsDto GenerateCredentials(string userId)
    {
        if (string.IsNullOrWhiteSpace(_settings.SharedSecret))
        {
            throw new InvalidOperationException("TURN shared secret is not configured.");
        }

        if (_settings.Uris == null || _settings.Uris.Length == 0)
        {
            throw new InvalidOperationException("TURN URIs are not configured.");
        }

        long expiryUnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _settings.TtlSeconds;
        string username = $"{expiryUnixTimestamp}:{userId}";

        string password = GenerateHmacSha1(username, _settings.SharedSecret);

        return new TurnCredentialsDto
        {
            Username = username,
            Password = password,
            Ttl = _settings.TtlSeconds,
            Uris = _settings.Uris
        };
    }

    private string GenerateHmacSha1(string username, string secret)
    {
        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA1(secretBytes);
        byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(username));
        return Convert.ToBase64String(hashBytes);
    }
}
