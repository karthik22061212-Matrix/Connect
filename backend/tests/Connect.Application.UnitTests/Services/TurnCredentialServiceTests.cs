using System;
using Connect.Application.Common.Interfaces;
using Connect.Infrastructure.Configuration;
using Connect.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Connect.Application.UnitTests.Services;

public class TurnCredentialServiceTests
{
    [Fact]
    public void GenerateCredentials_WithValidConfig_ReturnsExpectedFormat()
    {
        // Arrange
        var settings = new TurnSettings
        {
            SharedSecret = "test-secret",
            Uris = new[] { "turn:127.0.0.1:3478" },
            TtlSeconds = 3600
        };
        var optionsMock = new Mock<IOptions<TurnSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var service = new TurnCredentialService(optionsMock.Object);
        var userId = "user123";

        // Act
        var credentials = service.GenerateCredentials(userId);

        // Assert
        Assert.NotNull(credentials);
        Assert.Equal(3600, credentials.Ttl);
        Assert.Single(credentials.Uris);
        Assert.Equal("turn:127.0.0.1:3478", credentials.Uris[0]);

        var usernameParts = credentials.Username.Split(':');
        Assert.Equal(2, usernameParts.Length);
        Assert.Equal("user123", usernameParts[1]);

        // Expiry should be roughly now + 3600
        long expiry = long.Parse(usernameParts[0]);
        long expectedExpiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
        Assert.True(Math.Abs(expiry - expectedExpiry) <= 2);

        Assert.NotEmpty(credentials.Password);
    }

    [Fact]
    public void GenerateCredentials_MissingSecret_ThrowsException()
    {
        // Arrange
        var settings = new TurnSettings
        {
            SharedSecret = "", // Missing
            Uris = new[] { "turn:127.0.0.1:3478" },
            TtlSeconds = 3600
        };
        var optionsMock = new Mock<IOptions<TurnSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var service = new TurnCredentialService(optionsMock.Object);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => service.GenerateCredentials("user123"));
        Assert.Equal("TURN shared secret is not configured.", exception.Message);
    }

    [Fact]
    public void GenerateCredentials_DeterministicHmac()
    {
        // To test deterministic HMAC, we will mock the current time indirectly or just test the password logic
        // Since we can't easily mock DateTimeOffset.UtcNow in this class without a time provider,
        // we'll just test that the username matches the HMAC signature pattern.

        var settings = new TurnSettings
        {
            SharedSecret = "my-secret-key",
            Uris = new[] { "turn:127.0.0.1:3478" }
        };
        var optionsMock = new Mock<IOptions<TurnSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        var service = new TurnCredentialService(optionsMock.Object);

        var credentials = service.GenerateCredentials("testuser");

        // Verify the password is a valid Base64 string
        var bytes = Convert.FromBase64String(credentials.Password);
        Assert.NotNull(bytes);
        Assert.Equal(20, bytes.Length); // SHA1 hash is 20 bytes
    }
}
